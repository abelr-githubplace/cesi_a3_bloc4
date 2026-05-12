using SaveManager;
using Job;
using Actor;
using State;
using EasyLog;
using CryptoSoftRunner;
using BusinessSoftware;

namespace Save
{
    internal enum SaveType { Complete, Differential }

    public class Saver : SaveActor
    {
        // Cooperative pause/stop primitives. ManualResetEventSlim is set ("go") by
        // default and reset ("paused") on Pause(); Wait() suspends the worker
        // thread for free instead of polling. The CTS wakes a Wait() that's
        // blocked in the paused state when a Stop arrives — it doesn't interrupt
        // an in-progress file copy.
        private readonly ManualResetEventSlim _gate = new ManualResetEventSlim(true);
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public bool IsPaused => !_gate.IsSet;
        public bool IsStopped => _cts.IsCancellationRequested;

        // True while the worker is parked because the global watcher reported
        // a business-software process. The GUI consults this so a user
        // Resume doesn't flip the label to "Running" while the save is in
        // fact still parked.
        public bool IsWaitingForBusinessSoftware => !_bsGate.IsSet;

        // Independent gate driven by the BusinessSoftwareWatcher callback.
        // Set ("go") by default; reset ("wait") when the watcher reports a
        // process is running. The worker waits on this between files, just
        // like _gate handles user-driven pause.
        private readonly ManualResetEventSlim _bsGate = new ManualResetEventSlim(true);

        public Saver(SaveInfo save, SaveManager.Action saveAction, Progress.Progress progress, Config.ConfigManager configManager)
            : base(save, saveAction, progress, configManager)
        {

            long totalSize = 0;
            if (File.Exists(SourcePath) || Directory.Exists(SourcePath))
            {
                foreach (string file in GetAllFilesFullName(SourcePath))
                {
                    long fileSize = new FileInfo(file).Length;
                    FilesWithSizes[file] = fileSize;

                    string relativePath = File.Exists(SourcePath) ? Path.GetFileName(file) : Path.GetRelativePath(SourcePath, file);
                    string destFile = Path.Combine(DestinationPath, relativePath);
                    FileJob? job = CreateJob(
                        file, destFile, fileSize, saveAction == SaveManager.Action.DifferentialSave ? SaveType.Differential : SaveType.Complete
                    );
                    if (job is not null) Jobs.Add(job);

                    totalSize += fileSize;
                }
            }
            TotalSize = totalSize;
        }

        private FileJob? CreateJob(string sourceFile, string destFile, long fileSize, SaveType saveType)
        {
            var default_priority = Priority.Low;
            return saveType == SaveType.Differential
                ? new DifferentialSaveFileJob(sourceFile, destFile, destFile + ".diff", fileSize, default_priority)
                : new CompleteSaveFileJob(sourceFile, destFile, fileSize, default_priority);
        }

        public void Pause() => _gate.Reset();

        public void Resume() => _gate.Set();

        public void Stop()
        {
            _cts.Cancel();
            // Wake any worker currently sleeping in _gate.Wait() or
            // _bsGate.Wait() so it can observe cancellation and break out.
            _gate.Set();
            _bsGate.Set();
        }

        public void Start(bool paused)
        {
            if (paused) _gate.Reset();

            // Subscribe to the global business-software watcher. The callback
            // toggles _bsGate so the worker only proceeds when no watched
            // process is running. We also log the transitions so the daily
            // log keeps a trace of every auto-pause/resume the worker hits.
            bool wasRunning = false;
            using var bsSubscription = BusinessSoftwareWatcher.Get(_configManager).Subscribe(running =>
            {
                if (running == wasRunning) return;
                wasRunning = running;
                if (running)
                {
                    _bsGate.Reset();
                    LogBusinessSoftwareInterrupt();
                }
                else
                {
                    LogBusinessSoftwareResume();
                    _bsGate.Set();
                }
            });

            long copiedTotalBytes = 0;
            var endTime = DateTime.Now;

            for (int i = 0; i < Jobs.Count; i++)
            {
                // User-driven pause point: between files. Cahier des charges 2.0
                // says the current file must finish before the worker can react
                // to a pause/stop.
                if (!_gate.IsSet)
                {
                    _configManager.State.Save(NewStateInfo(
                        DateTime.Now, Status.Paused,
                        NewActiveStateInfo(Jobs.Count - i, this.TotalSize - copiedTotalBytes,
                            i < Jobs.Count ? Jobs[i].SourceFile : "",
                            i < Jobs.Count ? Jobs[i].DestinationFile : "")
                    ));
                    try { _gate.Wait(_cts.Token); }
                    catch (OperationCanceledException) { break; }
                }
                if (_cts.IsCancellationRequested) break;

                // Business-software gate. Driven by the watcher callback
                // above — we just block here without polling ourselves.
                if (!_bsGate.IsSet)
                {
                    _configManager.State.Save(NewStateInfo(
                        DateTime.Now, Status.Paused,
                        NewActiveStateInfo(Jobs.Count - i, this.TotalSize - copiedTotalBytes, Jobs[i].SourceFile, Jobs[i].DestinationFile)
                    ));
                    try { _bsGate.Wait(_cts.Token); }
                    catch (OperationCanceledException) { break; }
                }
                if (_cts.IsCancellationRequested) break;

                var job = Jobs[i];

                var beginTime = DateTime.Now;
                long copiedSize = job.Execute();
                endTime = DateTime.Now;

                if (copiedSize != job.FileSize) /* Error : does nothing for now, should be handled later on */;
                copiedTotalBytes += copiedSize;

                float percent = TotalSize <= 0 ? 100f : Math.Clamp(((float)copiedTotalBytes / (float)TotalSize) * 100f, 0f, 100f);
                Progress.SetProgress(percent);

                int encryptionTime = TryEncrypt(job.DestinationFile);

                _configManager.Logger.Log(
                    NewLogInfo(DateTime.Now, job.SourceFile, job.DestinationFile, job.FileSize, (endTime - beginTime).Milliseconds, encryptionTime)
                        .Format(_configManager.GetLogFormatConfig())
                );
                _configManager.State.Save(
                    NewStateInfo(
                        endTime,
                        Status.Active,
                        NewActiveStateInfo(Jobs.Count - i, this.TotalSize - copiedTotalBytes, job.SourceFile, job.DestinationFile)
                    )
                );
            }
            _configManager.State.Save(NewStateInfo(endTime, Status.Inactive, null));
        }

        // Returns 0 if encryption is disabled or the file is out of scope,
        // a positive ms count on success, or a negative error code from
        // CryptoSoftRunner.
        private int TryEncrypt(string destinationFile)
        {
            var extensions = _configManager.GetEncryptionExtensions();
            if (extensions.Count == 0) return 0;
            if (!Crypter.ShouldEncrypt(destinationFile, extensions)) return 0;
            return Crypter.EncryptFile(destinationFile, _configManager.GetEncryptionKey(), _configManager.GetCryptoSoftPath());
        }

        private void LogBusinessSoftwareInterrupt()
        {
            var watched = _configManager.GetBusinessSoftwares();
            var running = BusinessSoftware.BusinessSoftwareMonitor.GetRunningBusinessSoftware(watched);
            string detected = running.Count > 0 ? string.Join(",", running) : "unknown";

            _configManager.Logger.Log(
                new LogInfo
                {
                    DateTime = DateTime.Now,
                    SaveName = Name,
                    SourceFile = SourcePath,
                    DestinationFile = DestinationPath,
                    Action = "BUSINESS_SOFTWARE_PAUSE:" + detected,
                    FileSize = 0,
                    TransferTime = -1,
                    EncryptionTime = 0,
                }.Format(_configManager.GetLogFormatConfig())
            );
        }

        private void LogBusinessSoftwareResume()
        {
            _configManager.Logger.Log(
                new LogInfo
                {
                    DateTime = DateTime.Now,
                    SaveName = Name,
                    SourceFile = SourcePath,
                    DestinationFile = DestinationPath,
                    Action = "BUSINESS_SOFTWARE_RESUME",
                    FileSize = 0,
                    TransferTime = 0,
                    EncryptionTime = 0,
                }.Format(_configManager.GetLogFormatConfig())
            );
        }
    }
}
