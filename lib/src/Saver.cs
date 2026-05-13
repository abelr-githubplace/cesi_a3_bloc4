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
        private readonly CancellationTokenSource _cts;

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

        // V3: per-job parallelism cap comes from config (clamped there).

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

            // V3 priority snapshot. Frozen for the whole Start() so the
            // semantics of this save stay consistent even if the user edits
            // the priority list mid-run. We register every priority file we
            // *plan* to copy in the global gate now; non-priority workers of
            // OTHER savers will wait until our priorities have all begun.
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

            // Move priority files to the front of the queue so workers pick
            // them up first. Without this, Parallel.ForEach could park every
            // worker on the priority gate, waiting for priority files that
            // are still later in the (unsorted) list — a deadlock partial,
            // since those workers are the only ones that could process them.
            Jobs.Sort((a, b) =>
            {
                bool aPrio = IsPriorityFile(a);
                bool bPrio = IsPriorityFile(b);
                if (aPrio == bPrio) return 0;
                return aPrio ? -1 : 1;
            });

            // Shared state across all workers of this job. Bytes and counts go
            // through Interlocked, the "currently in flight" registry is a
            // ConcurrentDictionary, state.json writes are serialized with a
            // lock so two workers don't produce interleaved snapshots.
            long copiedTotalBytes = 0;
            int filesProcessed = 0;
            var currentFiles = new ConcurrentDictionary<string, string>();
            var stateLock = new object();

            // Time-based throttles. Progress UI capped at ~30 fps (33 ms)
            // so the bar stays smooth even on small files where the CAS-on-
            // integer-percent strategy made it look like discrete jumps.
            // State.json capped at ~5 updates per second (200 ms) — the
            // worst case is a save with thousands of tiny files, where
            // unthrottled persistence would hammer the disk.
            long lastProgressTicks = 0;
            long lastStateWriteTicks = 0;
            long progressIntervalTicks = TimeSpan.FromMilliseconds(33).Ticks;
            long stateWriteIntervalTicks = TimeSpan.FromMilliseconds(200).Ticks;

            // Per-chunk callback. Called from worker threads (potentially
            // multiple at once) every 64 KB. We add to the global counter
            // atomically, then throttle the actual UI notification to ~30
            // updates per second using a CAS-on-timestamp pattern.
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

            // Same throttling pattern for state.json. Used both before and
            // after a file's Execute so the snapshot reflects "what's in
            // flight" at a steady rate without flooding the disk.
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

            // NoBuffering = workers fetch one item at a time instead of
            // grabbing a chunk of, say, 25 items upfront. Combined with the
            // priority sort above, this guarantees the first N items fetched
            // (one per worker) are all priority files when there are any.
            var partitioner = System.Collections.Concurrent.Partitioner.Create(
                Jobs, System.Collections.Concurrent.EnumerablePartitionerOptions.NoBuffering);

            try
            {
                Parallel.ForEach(partitioner, options, fileJob =>
                {
                    // Pause gates BEFORE starting a new file. Each worker
                    // checks its own gates so a Pause()/auto-pause applies to
                    // every worker simultaneously.
                    try { _gate.Wait(_cts.Token); _bsGate.Wait(_cts.Token); }
                    catch (OperationCanceledException) { return; }
                    if (_cts.IsCancellationRequested) return;

                    // Priority gate. A priority file leaves the pending pool
                    // as soon as it begins copying (the cahier says "en
                    // attente" = not yet started). A non-priority file must
                    // wait until the global pending count reaches 0.
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

                    // V3 large-file gate: serialize transfers of any file
                    // bigger than the configured threshold across the whole
                    // process. Re-read the threshold per file so a mid-save
                    // config change takes effect on the next acquisition.
                    int thresholdKB = _configManager.GetLargeFileThresholdKB();
                    IDisposable largeFileScope;
                    try { largeFileScope = LargeFileGate.AcquireIfLarge(fileJob.FileSize, thresholdKB, _cts.Token); }
                    catch (OperationCanceledException) { return; }

                    currentFiles[fileJob.SourceFile] = fileJob.DestinationFile;
                    // Snapshot the "in flight" file list BEFORE the long
                    // Execute so the state file reflects what's currently
                    // being copied, not what was copied minutes ago.
                    TryPersistActive();

                    try
                    {
                        var beginTime = DateTime.Now;
                        long copiedSize;
                        try { copiedSize = fileJob.Execute(_cts.Token, onProgress); }
                        catch (OperationCanceledException)
                        {
                            // Mid-copy Stop. CopyChunked already removed the
                            // partial destination file. Don't log / persist a
                            // success state for this file — just bail out.
                            currentFiles.TryRemove(fileJob.SourceFile, out _);
                            return;
                        }
                        var endTime = DateTime.Now;

                        // copiedTotalBytes was already incremented chunk by
                        // chunk inside onProgress, so no Interlocked.Add here.
                        Interlocked.Increment(ref filesProcessed);

                        int encryptionTime = TryEncrypt(fileJob.DestinationFile);

                        _configManager.Logger.Log(
                            NewLogInfo(DateTime.Now, fileJob.SourceFile, fileJob.DestinationFile,
                                fileJob.FileSize, (endTime - beginTime).Milliseconds, encryptionTime)
                                .Format(_configManager.GetLogFormatConfig())
                        );

                        currentFiles.TryRemove(fileJob.SourceFile, out _);
                        // Throttled persistence after each file too — covers
                        // the counter (FilesRemaining) and removes the now-
                        // finished file from CurrentFiles in the snapshot.
                        TryPersistActive();
                    }
                    catch (Exception ex)
                    {
                        // Log any unexpected exception so the save loop doesn't
                        // die silently. AggregateException from Parallel will
                        // wrap whatever escapes here, but at least we trace it.
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
            catch (OperationCanceledException) { /* expected on Stop */ }
            catch (Exception ex)
            {
                // Don't let an unexpected worker exception kill the save loop
                // silently — log it and continue to the cleanup below so the
                // final Inactive state is still written and the priority gate
                // is still released.
                try { _configManager.Logger.Log($"[Saver Start error] {ex}"); } catch { }
            }
            finally
            {
                // Release any priority tickets we registered but never
                // reached (Stop fired before some iterations could start).
                // Without this, non-priority workers of other savers would
                // be stuck waiting on a counter that never reaches 0.
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
