using SaveManager;
using StateManager;
using Job;
using Observer;
using SaveInterrupt;

namespace Saver
{
    public class Progress : IPublisher
    {
        private List<ISubscriber> _subscribers;
        private float _progress;

        public Progress()
        {
            _progress = 0;
            _subscribers = new List<ISubscriber>();
        }

        public void Subscribe(ISubscriber subscriber)
        {
            if (!_subscribers.Contains(subscriber)) _subscribers.Add(subscriber);
        }

        public void Unsubscribe(ISubscriber subscriber)
        {
            if (_subscribers.Contains(subscriber)) _subscribers.Remove(subscriber);
        }

        public void Notify()
        {
            foreach (var subscriber in _subscribers) subscriber.Update();
        }

        public float GetProgress()
        {
            return _progress;
        }

        public void SetProgress(float progress)
        {
            _progress = progress;
            Notify();
        }
    }

    public class Saver
    {
        public Guid Id { get; set; }
        public string Name { get; }
        public string SourcePath { get; }
        public string DestinationPath { get; }
        public long TotalSize { get; }
        public Dictionary<string, long> FilesWithSizes { get; }
        public Progress Progress { get; }

        protected List<SaveJob> Jobs;
        private Config _config;

        // Cooperative pause/stop primitives. ManualResetEventSlim is set ("go") by
        // default and reset ("paused") by the PauseListener; Wait() suspends the
        // worker thread for free instead of polling. The CTS is only used to wake
        // a Wait() that's blocked in the paused state when a Stop arrives — it
        // doesn't interrupt an in-progress file copy.
        private readonly ManualResetEventSlim _gate = new ManualResetEventSlim(true);
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public bool IsPaused => !_gate.IsSet;
        public bool IsStopped => _cts.IsCancellationRequested;

        // Internal listeners. Private nested classes so each Saver has its
        // own ISubscriber identity — the publisher dispatches by target
        // without needing a payload on Update().
        //
        // The PauseListener doesn't toggle the gate blindly: it reads its
        // Pauser's IsPaused flag and mirrors it. That way every Saver
        // subscribed to the same Pauser stays in sync, regardless of when
        // it was attached.
        private class PauseListener : ISubscriber
        {
            private readonly Saver _owner;
            private readonly Pauser _pauser;
            public PauseListener(Saver owner, Pauser pauser) { _owner = owner; _pauser = pauser; }
            public void Update()
            {
                if (_pauser.IsPaused) _owner._gate.Reset();
                else _owner._gate.Set();
            }
        }

        private class StopListener : ISubscriber
        {
            private readonly Saver _owner;
            public StopListener(Saver owner) { _owner = owner; }
            public void Update()
            {
                _owner._cts.Cancel();
                // Wake any worker currently sleeping in _gate.Wait() so it can
                // observe cancellation and break out.
                _owner._gate.Set();
            }
        }

        public Saver(SaveInfo save, SaveType saveType, Progress progress, Config config, Pauser pauser, Stopper stopper)
        {
            Id = save.SaveId;
            Name = save.SaveName;
            SourcePath = save.SourcePath;
            DestinationPath = save.DestinationPath;
            Progress = progress;
            _config = config;

            // Plug the two internal listeners into the publishers. From now on
            // pause/resume/stop are driven exclusively through the observer chain:
            //   pauser.Pause()  -> Notify() -> PauseListener.Update() -> _gate.Reset()
            //   pauser.Resume() -> Notify() -> PauseListener.Update() -> _gate.Set()
            //   stopper.Stop()  -> Notify() -> StopListener.Update()  -> _cts.Cancel()
            pauser.Subscribe(new PauseListener(this, pauser));
            stopper.Subscribe(new StopListener(this));

            Jobs = new List<SaveJob>();
            FilesWithSizes = new Dictionary<string, long>();

            long totalSize = 0;
            if (File.Exists(SourcePath) || Directory.Exists(SourcePath))
            {
                foreach (string file in GetAllFilesFullName(SourcePath))
                {
                    // Populate File to FileSize hashmap
                    long fileSize = new FileInfo(file).Length;
                    FilesWithSizes[file] = fileSize;

                    // Create Jobs
                    string relativePath = File.Exists(SourcePath) ? Path.GetFileName(file) : Path.GetRelativePath(SourcePath, file);
                    string destFile = Path.Combine(DestinationPath, relativePath);
                    var job = CreateJob(file, destFile, fileSize, saveType);
                    if (job != null) Jobs.Add(job);

                    totalSize += fileSize;
                }
            }
            TotalSize = totalSize;
        }

        private SaveJob? CreateJob(string sourceFile, string destFile, long fileSize, SaveType saveType)
        {
            var default_priority = Priority.Medium;
            switch (saveType)
            {
                case SaveType.Complete: return new Job.CompleteSaveJob(sourceFile, destFile, fileSize, default_priority);
                case SaveType.Differential: return new Job.DifferentialSaveJob(sourceFile, destFile, fileSize, default_priority);
                default: return null;
            }
        }

        private static bool IsFile(string path)
        {
            FileAttributes attr = File.GetAttributes(path);
            if (!attr.HasFlag(FileAttributes.Directory)) return true;
            return false;
        }

        private static IEnumerable<string> GetAllFilesFullName(string path)
        {
            if (IsFile(path))
            {
                var list = new List<string>();
                list.Add(path);
                return list;
            }
            return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories);
        }

        public void Start()
        {
            long copiedTotalBytes = 0;
            var endTime = DateTime.Now;

            for (int i = 0; i < Jobs.Count; i++)
            {
                // Pause point: between files. Cahier des charges 2.0 says the current
                // file must finish before the worker can react to a pause/stop, so
                // the cooperative check sits between iterations rather than mid-copy.
                if (!_gate.IsSet)
                {
                    PersistRunningState(i, copiedTotalBytes, Status.Paused);
                    try { _gate.Wait(_cts.Token); }
                    catch (OperationCanceledException) { break; }
                }
                if (_cts.IsCancellationRequested) break;

                // Business software pause point. 3.0-style auto-pause/resume:
                // if a watched process is running, log + persist a Paused
                // state, then poll every second until it stops, then log +
                // persist a resume. The check sits between files so the
                // in-progress file always completes (cahier des charges
                // "terminer le fichier en cours").
                if (IsBusinessSoftwareDetected())
                {
                    LogBusinessSoftwareInterrupt();
                    PersistRunningState(i, copiedTotalBytes, Status.Paused);

                    while (IsBusinessSoftwareDetected())
                    {
                        if (_cts.IsCancellationRequested) break;
                        try { Task.Delay(1000, _cts.Token).Wait(); }
                        catch (Exception) { break; }
                    }
                    if (_cts.IsCancellationRequested) break;

                    LogBusinessSoftwareResume();
                    PersistRunningState(i, copiedTotalBytes, Status.Active);
                }

                var job = Jobs[i];

                var beginTime = DateTime.Now;
                long copiedSize = job.Execute(BuildEncryptionContext());
                endTime = DateTime.Now;

                if (copiedSize != job.FileSize) /* Error : does nothing for now, should be handled later on */;
                copiedTotalBytes += copiedSize;

                float percent = TotalSize <= 0 ? 100f : Math.Clamp(((float)copiedTotalBytes / (float)TotalSize) * 100f, 0f, 100f);
                Progress.SetProgress(percent);

                int encryptionTime = TryEncrypt(job.DestinationFile);

                _config.Logger.Log(
                    new EasyLog.LogInfo {
                        DateTime = DateTime.Now,
                        SaveName = Name,
                        SourceFile = job.SourceFile,
                        DestinationFile = job.DestinationFile,
                        Action = "SAVE",
                        FileSize = job.FileSize,
                        TransferTime = (endTime - beginTime).Milliseconds,
                        EncryptionTime = encryptionTime,
                    }.Format(_config.LogFormat)
                );
                _config.StateManager.Save(
                    new SaveState {
                        Id = this.Id,
                        Name = this.Name,
                        SourcePath = this.SourcePath,
                        DestinationPath = this.DestinationPath,
                        LastActionTime = endTime,
                        Status = Status.Active,
                        ActiveStateInfo = new ActiveStateInfo {
                            TotalFiles = this.FilesWithSizes.Count,
                            TotalSize = this.TotalSize,
                            FilesRemaining = Jobs.Count - i,
                            SizeRemaining = this.TotalSize - copiedTotalBytes,
                            Progress = this.Progress.GetProgress(),
                            CurrentSourceFile = job.SourceFile,
                            CurrentTargetFile = job.DestinationFile
                        }
                    }
                );
            }

            _config.StateManager.Save(
                    new SaveState
                    {
                        Id = this.Id,
                        Name = this.Name,
                        SourcePath = this.SourcePath,
                        DestinationPath = this.DestinationPath,
                        LastActionTime = endTime,
                        Status = Status.Inactive,
                        ActiveStateInfo = null,
                    }
                );
        }

        // Returns 0 if encryption is disabled or the file is not in scope,
        // a positive ms count on success, or a negative error code from EasyCrypt.
        private int TryEncrypt(string destinationFile)
        {
            var ctx = BuildEncryptionContext();
            if (ctx == null) return 0;
            if (!ctx.ShouldEncrypt(destinationFile)) return 0;
            return EasyCrypt.Crypter.EncryptFile(destinationFile, ctx.Key, ctx.CryptoSoftPath);
        }

        // Single source of truth for "what does this Saver know about
        // encryption" — null means encryption is disabled (no AppConfig
        // attached or no extensions configured).
        private Job.EncryptionContext? BuildEncryptionContext()
        {
            var appConfig = _config.AppConfig;
            if (appConfig == null) return null;
            var extensions = appConfig.GetEncryptionExtensions();
            if (extensions.Count == 0) return null;
            return new Job.EncryptionContext
            {
                Extensions = extensions,
                Key = appConfig.GetEncryptionKey(),
                CryptoSoftPath = appConfig.GetCryptoSoftPath(),
            };
        }

        private bool IsBusinessSoftwareDetected()
        {
            if (_config.AppConfig == null) return false;
            var watched = _config.AppConfig.GetBusinessSoftware();
            if (watched.Count == 0) return false;
            return BusinessSoftwareMonitor.BusinessSoftwareMonitor.IsAnyRunning(watched);
        }

        private void LogBusinessSoftwareInterrupt()
        {
            var watched = _config.AppConfig?.GetBusinessSoftware();
            var running = watched == null
                ? new List<string>()
                : BusinessSoftwareMonitor.BusinessSoftwareMonitor.GetRunningBusinessSoftware(watched);
            string detected = running.Count > 0 ? string.Join(",", running) : "unknown";

            _config.Logger.Log(
                new EasyLog.LogInfo
                {
                    DateTime = DateTime.Now,
                    SaveName = Name,
                    SourceFile = SourcePath,
                    DestinationFile = DestinationPath,
                    Action = "BUSINESS_SOFTWARE_PAUSE:" + detected,
                    FileSize = 0,
                    TransferTime = -1,
                }.Format(_config.LogFormat)
            );
        }

        private void LogBusinessSoftwareResume()
        {
            _config.Logger.Log(
                new EasyLog.LogInfo
                {
                    DateTime = DateTime.Now,
                    SaveName = Name,
                    SourceFile = SourcePath,
                    DestinationFile = DestinationPath,
                    Action = "BUSINESS_SOFTWARE_RESUME",
                    FileSize = 0,
                    TransferTime = 0,
                }.Format(_config.LogFormat)
            );
        }

        private void PersistRunningState(int nextJobIndex, long copiedTotalBytes, Status status)
        {
            string currentSrc = nextJobIndex < Jobs.Count ? Jobs[nextJobIndex].SourceFile : "";
            string currentDst = nextJobIndex < Jobs.Count ? Jobs[nextJobIndex].DestinationFile : "";
            _config.StateManager.Save(
                new SaveState
                {
                    Id = this.Id,
                    Name = this.Name,
                    SourcePath = this.SourcePath,
                    DestinationPath = this.DestinationPath,
                    LastActionTime = DateTime.Now,
                    Status = status,
                    ActiveStateInfo = new ActiveStateInfo
                    {
                        TotalFiles = this.FilesWithSizes.Count,
                        TotalSize = this.TotalSize,
                        FilesRemaining = Jobs.Count - nextJobIndex,
                        SizeRemaining = this.TotalSize - copiedTotalBytes,
                        Progress = this.Progress.GetProgress(),
                        CurrentSourceFile = currentSrc,
                        CurrentTargetFile = currentDst,
                    }
                }
            );
        }
    }

    // Restorer is intentionally NOT wired into the observer-based pause/stop
    // system for now. It runs straight through. When the team is ready to
    // extend the Pause/Stop control surface to restores, the same pattern as
    // Saver applies: take a Pauser and a Stopper in the ctor, subscribe two
    // listeners, gate the loop between files.
    public class Restorer
    {
        public Guid Id { get; }
        public string Name { get; }
        public string SourcePath { get; }
        public string DestinationPath { get; }
        public long TotalSize { get; }
        public Progress Progress { get; }

        protected List<RestoreJob> Jobs;
        // Optional. When null, restore is a plain CopyBack with no decryption —
        // matches the legacy behavior for callers that don't care about crypto.
        private readonly Config? _config;

        public Restorer(SaveInfo save, Progress progress) : this(save, progress, null) { }

        public Restorer(SaveInfo save, Progress progress, Config? config)
        {
            Id = save.SaveId;
            Name = save.SaveName;
            SourcePath = save.SourcePath;
            DestinationPath = save.DestinationPath;
            Progress = progress;
            _config = config;
            Jobs = new List<RestoreJob>();

            long totalSize = 0;
            if (File.Exists(DestinationPath) || Directory.Exists(DestinationPath))
            {
                foreach (string file in GetRestorableFiles(DestinationPath))
                {
                    long fileSize = new FileInfo(file).Length;
                    string relativePath = File.Exists(DestinationPath)
                        ? Path.GetFileName(file)
                        : Path.GetRelativePath(DestinationPath, file);
                    string srcFile = Path.Combine(SourcePath, relativePath);

                    string diffFile = file + ".diff";
                    RestoreJob job = File.Exists(diffFile)
                        ? new DifferentialRestoreJob(srcFile, file, diffFile, fileSize)
                        : new CompleteRestoreJob(srcFile, file, fileSize);

                    Jobs.Add(job);
                    totalSize += fileSize;
                }
            }
            TotalSize = totalSize;
        }

        private static IEnumerable<string> GetRestorableFiles(string path)
        {
            if (File.Exists(path))
            {
                if (path.EndsWith(".diff", StringComparison.Ordinal)) return Array.Empty<string>();
                return new[] { path };
            }
            if (!Directory.Exists(path)) return Array.Empty<string>();
            // .diff files are sidecars consumed by their matching base file — skip them here.
            return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".diff", StringComparison.Ordinal));
        }

        public void Start()
        {
            long restoredTotalBytes = 0;
            for (int i = 0; i < Jobs.Count; i++)
            {
                var job = Jobs[i];
                long restored = job.Execute(BuildEncryptionContext());
                restoredTotalBytes += restored;

                float percent = TotalSize <= 0 ? 100f : Math.Clamp(((float)restoredTotalBytes / (float)TotalSize) * 100f, 0f, 100f);
                Progress.SetProgress(percent);
            }
        }

        // Same logic as on the Saver side: null when there's no AppConfig or
        // no extensions configured. Restorer accepts a null Config for legacy
        // callers (CLI / pre-2.0 tests).
        private Job.EncryptionContext? BuildEncryptionContext()
        {
            if (_config == null) return null;
            var appConfig = _config.AppConfig;
            if (appConfig == null) return null;
            var extensions = appConfig.GetEncryptionExtensions();
            if (extensions.Count == 0) return null;
            return new Job.EncryptionContext
            {
                Extensions = extensions,
                Key = appConfig.GetEncryptionKey(),
                CryptoSoftPath = appConfig.GetCryptoSoftPath(),
            };
        }
    }
}
