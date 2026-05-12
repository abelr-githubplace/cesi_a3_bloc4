using System.Collections.Concurrent;
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

        // V3: up to MaxWorkersPerJob files of the same job are copied in
        // parallel. Fixed at 4 for now — the bottleneck is disk/network, not
        // CPU, and going higher mostly thrashes the disk. Make paramétrable
        // later if a client asks.
        private const int MaxWorkersPerJob = 4;

        public void Start(bool paused)
        {
            if (paused) _gate.Reset();

            // Subscribe to the global business-software watcher. The callback
            // toggles _bsGate so workers only proceed when no watched process
            // is running. We log the transitions so the daily log keeps a
            // trace of every auto-pause/resume the job hits.
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

            // Shared state across all workers of this job. Bytes and counts go
            // through Interlocked, the "currently in flight" registry is a
            // ConcurrentDictionary, state.json writes are serialized with a
            // lock so two workers don't produce interleaved snapshots.
            long copiedTotalBytes = 0;
            int filesProcessed = 0;
            var currentFiles = new ConcurrentDictionary<string, string>();
            var stateLock = new object();

            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxWorkersPerJob,
                CancellationToken = _cts.Token,
            };

            try
            {
                Parallel.ForEach(Jobs, options, fileJob =>
                {
                    // Pause gates BEFORE starting a new file. Each worker
                    // checks its own gates so a Pause()/auto-pause applies to
                    // every worker simultaneously.
                    try { _gate.Wait(_cts.Token); _bsGate.Wait(_cts.Token); }
                    catch (OperationCanceledException) { return; }
                    if (_cts.IsCancellationRequested) return;

                    // V3 large-file gate: serialize transfers of any file
                    // bigger than the configured threshold across the whole
                    // process. Re-read the threshold per file so a mid-save
                    // config change takes effect on the next acquisition.
                    int thresholdKB = _configManager.GetLargeFileThresholdKB();
                    IDisposable largeFileScope;
                    try { largeFileScope = LargeFileGate.AcquireIfLarge(fileJob.FileSize, thresholdKB, _cts.Token); }
                    catch (OperationCanceledException) { return; }

                    currentFiles[fileJob.SourceFile] = fileJob.DestinationFile;
                    try
                    {
                        var beginTime = DateTime.Now;
                        long copiedSize = fileJob.Execute();
                        var endTime = DateTime.Now;

                        long newTotal = Interlocked.Add(ref copiedTotalBytes, copiedSize);
                        int newCount = Interlocked.Increment(ref filesProcessed);

                        float percent = TotalSize <= 0 ? 100f
                            : Math.Clamp(((float)newTotal / (float)TotalSize) * 100f, 0f, 100f);
                        Progress.SetProgress(percent);

                        int encryptionTime = TryEncrypt(fileJob.DestinationFile);

                        _configManager.Logger.Log(
                            NewLogInfo(DateTime.Now, fileJob.SourceFile, fileJob.DestinationFile,
                                fileJob.FileSize, (endTime - beginTime).Milliseconds, encryptionTime)
                                .Format(_configManager.GetLogFormatConfig())
                        );

                        currentFiles.TryRemove(fileJob.SourceFile, out _);

                        lock (stateLock)
                        {
                            _configManager.State.Save(NewStateInfo(
                                endTime, Status.Active,
                                NewActiveStateInfo(
                                    Jobs.Count - newCount,
                                    this.TotalSize - newTotal,
                                    SnapshotCurrentFiles(currentFiles))
                            ));
                        }
                    }
                    catch
                    {
                        currentFiles.TryRemove(fileJob.SourceFile, out _);
                        throw;
                    }
                    finally
                    {
                        largeFileScope.Dispose();
                    }
                });
            }
            catch (OperationCanceledException) { /* expected on Stop */ }

            _configManager.State.Save(NewStateInfo(DateTime.Now, Status.Inactive, null));
        }

        private static List<ActiveFileInfo> SnapshotCurrentFiles(ConcurrentDictionary<string, string> dict)
        {
            var list = new List<ActiveFileInfo>(dict.Count);
            foreach (var kvp in dict)
                list.Add(new ActiveFileInfo { Source = kvp.Key, Target = kvp.Value });
            return list;
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
