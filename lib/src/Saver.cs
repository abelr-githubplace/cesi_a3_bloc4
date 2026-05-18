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
    internal enum SaveType { Complete, Differential, Delta }

    public class Saver : SaveActor
    {
        private readonly ManualResetEventSlim _gate = new ManualResetEventSlim(true);
        private readonly CancellationTokenSource _cts;

        public bool IsPaused => !_gate.IsSet;
        public bool IsStopped => _cts.IsCancellationRequested;

        public bool IsWaitingForBusinessSoftware => !_bsGate.IsSet;

        private readonly ManualResetEventSlim _bsGate = new ManualResetEventSlim(true);

        public Saver(SaveInfo save, SaveManager.Action saveAction, Progress.Progress progress, Config.ConfigManager configManager, CancellationTokenSource? cts = null)
            : base(save, saveAction, progress, configManager)
        {
            _cts = cts ?? new CancellationTokenSource();

            long totalSize = 0;
            if (File.Exists(SourcePath) || Directory.Exists(SourcePath))
            {
                foreach (string file in GetAllFilesFullName(SourcePath))
                {
                    long fileSize = new FileInfo(file).Length;
                    FilesWithSizes[file] = fileSize;

                    string relativePath = File.Exists(SourcePath) ? Path.GetFileName(file) : Path.GetRelativePath(SourcePath, file);
                    string destFile = Path.Combine(DestinationPath, relativePath);
                    var saveType = saveAction switch
                    {
                        SaveManager.Action.DifferentialSave => SaveType.Differential,
                        SaveManager.Action.DeltaSave => SaveType.Delta,
                        _ => SaveType.Complete
                    };
                    FileJob? job = CreateJob(file, destFile, fileSize, saveType);
                    if (job is not null) Jobs.Add(job);

                    totalSize += fileSize;
                }
            }
            TotalSize = totalSize;
        }

        private FileJob? CreateJob(string sourceFile, string destFile, long fileSize, SaveType saveType)
        {
            var priority = Priority.Low;
            return saveType switch
            {
                SaveType.Differential => new DifferentialSaveFileJob(sourceFile, destFile, fileSize, priority),
                SaveType.Delta => new DeltaSaveFileJob(sourceFile, destFile, destFile + ".diff", fileSize, priority),
                _ => new CompleteSaveFileJob(sourceFile, destFile, fileSize, priority)
            };
        }

        public void Pause() => _gate.Reset();

        public void Resume() => _gate.Set();

        public void Stop()
        {
            _cts.Cancel();
            _gate.Set();
            _bsGate.Set();
        }

        public void Start(bool paused)
        {
            if (paused) _gate.Reset();

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

            var prioritySet = new HashSet<string>(
                _configManager.GetPriorityExtensions().Select(e => e.ToLowerInvariant()));
            bool IsPriorityFile(FileJob fj)
            {
                if (prioritySet.Count == 0) return false;
                var ext = Path.GetExtension(fj.SourceFile).ToLowerInvariant();
                return !string.IsNullOrEmpty(ext) && prioritySet.Contains(ext);
            }
            int priorityCount = Jobs.Count(IsPriorityFile);
            PriorityGate.AddPending(priorityCount);
            int prioritiesConsumed = 0;

            Jobs.Sort((a, b) =>
            {
                bool aPrio = IsPriorityFile(a);
                bool bPrio = IsPriorityFile(b);
                if (aPrio == bPrio) return 0;
                return aPrio ? -1 : 1;
            });

            long copiedTotalBytes = 0;
            int filesProcessed = 0;
            var currentFiles = new ConcurrentDictionary<string, string>();
            var stateLock = new object();

            long lastProgressTicks = 0;
            long lastStateWriteTicks = 0;
            long progressIntervalTicks = TimeSpan.FromMilliseconds(33).Ticks;
            long stateWriteIntervalTicks = TimeSpan.FromMilliseconds(200).Ticks;

            Action<long> onProgress = (deltaBytes) =>
            {
                if (deltaBytes <= 0) return;
                long newTotal = Interlocked.Add(ref copiedTotalBytes, deltaBytes);
                if (TotalSize <= 0) { Progress.SetProgress(100f); return; }
                float pct = Math.Clamp(((float)newTotal / (float)TotalSize) * 100f, 0f, 100f);

                long now = DateTime.Now.Ticks;
                long previous = Volatile.Read(ref lastProgressTicks);
                if (now - previous < progressIntervalTicks) return;
                if (Interlocked.CompareExchange(ref lastProgressTicks, now, previous) != previous) return;
                Progress.SetProgress(pct);
            };

            bool TryPersistActive()
            {
                long now = DateTime.Now.Ticks;
                long previous = Volatile.Read(ref lastStateWriteTicks);
                if (now - previous < stateWriteIntervalTicks) return false;
                if (Interlocked.CompareExchange(ref lastStateWriteTicks, now, previous) != previous) return false;
                lock (stateLock)
                {
                    _configManager.State.Save(NewStateInfo(
                        DateTime.Now, Status.Active,
                        NewActiveStateInfo(
                            Jobs.Count - Volatile.Read(ref filesProcessed),
                            this.TotalSize - Interlocked.Read(ref copiedTotalBytes),
                            SnapshotCurrentFiles(currentFiles))
                    ));
                }
                return true;
            }

            int maxWorkers = Math.Max(1, _configManager.GetMaxWorkersPerJob());
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxWorkers,
                CancellationToken = _cts.Token,
            };

            var partitioner = System.Collections.Concurrent.Partitioner.Create(
                Jobs, System.Collections.Concurrent.EnumerablePartitionerOptions.NoBuffering);

            try
            {
                Parallel.ForEach(partitioner, options, fileJob =>
                {
                    try { _gate.Wait(_cts.Token); _bsGate.Wait(_cts.Token); }
                    catch (OperationCanceledException) { return; }
                    if (_cts.IsCancellationRequested) return;

                    bool isPriority = IsPriorityFile(fileJob);
                    if (isPriority)
                    {
                        PriorityGate.MarkOneStarted();
                        Interlocked.Increment(ref prioritiesConsumed);
                    }
                    else
                    {
                        try { PriorityGate.WaitForAllPending(_cts.Token); }
                        catch (OperationCanceledException) { return; }
                    }

                    int thresholdKB = _configManager.GetLargeFileThresholdKB();
                    IDisposable largeFileScope;
                    try { largeFileScope = LargeFileGate.AcquireIfLarge(fileJob.FileSize, thresholdKB, _cts.Token); }
                    catch (OperationCanceledException) { return; }

                    currentFiles[fileJob.SourceFile] = fileJob.DestinationFile;
                    TryPersistActive();

                    try
                    {
                        var beginTime = DateTime.Now;
                        long copiedSize;
                        try { copiedSize = fileJob.Execute(_cts.Token, onProgress); }
                        catch (OperationCanceledException)
                        {
                            currentFiles.TryRemove(fileJob.SourceFile, out _);
                            return;
                        }
                        var endTime = DateTime.Now;

                        Interlocked.Increment(ref filesProcessed);

                        int encryptionTime = TryEncrypt(fileJob.DestinationFile);

                        _configManager.Logger.Log(
                            NewLogInfo(DateTime.Now, fileJob.SourceFile, fileJob.DestinationFile,
                                fileJob.FileSize, (endTime - beginTime).Milliseconds, encryptionTime)
                                .Format(_configManager.GetLogFormatConfig())
                        );

                        currentFiles.TryRemove(fileJob.SourceFile, out _);
                        TryPersistActive();
                    }
                    catch (Exception ex)
                    {
                        try { _configManager.Logger.Log($"[Saver error] {ex}"); } catch { }
                        currentFiles.TryRemove(fileJob.SourceFile, out _);
                        throw;
                    }
                    finally
                    {
                        largeFileScope.Dispose();
                    }
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                try { _configManager.Logger.Log($"[Saver Start error] {ex}"); } catch { }
            }
            finally
            {
                // Unreleased tickets stall non-priority workers of other savers indefinitely.
                int leftover = priorityCount - Volatile.Read(ref prioritiesConsumed);
                if (leftover > 0) PriorityGate.MarkManyStarted(leftover);
            }

            _configManager.State.Save(NewStateInfo(DateTime.Now, Status.Inactive, null));
        }

        private static List<ActiveFileInfo> SnapshotCurrentFiles(ConcurrentDictionary<string, string> dict)
        {
            var list = new List<ActiveFileInfo>(dict.Count);
            foreach (var kvp in dict)
                list.Add(new ActiveFileInfo { Source = kvp.Key, Target = kvp.Value });
            return list;
        }

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
