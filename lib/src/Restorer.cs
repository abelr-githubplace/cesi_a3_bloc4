using Actor;
using Config;
using CryptoSoftRunner;
using Job;
using SaveManager;
using State;

namespace Restore
{
    public sealed class Restorer : SaveActor
    {
        public Restorer(SaveInfo save, SaveManager.Action saveAction, Progress.Progress progress, ConfigManager configManager)
            : base(save, saveAction, progress, configManager)
        {
            long totalSize = 0;

            // Determine which files in the backup belong to THIS save.
            // Saver wrote: if source was a single FILE → backup is DestinationPath\filename
            //              if source was a DIRECTORY  → backup is DestinationPath\relative\...
            // We mirror that logic to enumerate only the relevant files.
            IEnumerable<string> backupFiles;
            bool sourceWasFile = !Directory.Exists(SourcePath);   // dir deleted or was a file

            if (sourceWasFile)
            {
                // Single-file save: only the one file whose name matches the source filename.
                string expected = Path.Combine(DestinationPath, Path.GetFileName(SourcePath));
                backupFiles = File.Exists(expected) ? [expected] : [];
            }
            else
            {
                backupFiles = Directory.Exists(DestinationPath)
                    ? Directory.EnumerateFiles(DestinationPath, "*", SearchOption.AllDirectories)
                    : [];
            }

            foreach (string file in backupFiles.Where(f => !f.EndsWith(".diff", StringComparison.Ordinal)))
            {
                long fileSize = new FileInfo(file).Length;
                FilesWithSizes[file] = fileSize;

                // Mirror Saver: single-file → restore to SourcePath directly;
                // directory → restore to the relative path within SourcePath.
                string sourceFile = sourceWasFile
                    ? SourcePath
                    : Path.Combine(SourcePath, Path.GetRelativePath(DestinationPath, file));

                FileJob job = CreateJob(sourceFile, file, fileSize);
                Jobs.Add(job);
                totalSize += fileSize;
            }

            TotalSize = totalSize;
        }

        private static FileJob CreateJob(string sourceFile, string destFile, long fileSize)
        {
            var default_priority = Priority.Low;
            return File.Exists(destFile + ".diff")
                ? new DifferentialRestoreFileJob(sourceFile, destFile, destFile + ".diff", fileSize, default_priority)
                : new CompleteRestoreFileJob(sourceFile, destFile, fileSize, default_priority);
        }

        public void Start()
        {
            long restoredTotalBytes = 0;
            var endTime = DateTime.Now;

            try
            {
                for (int i = 0; i < Jobs.Count; i++)
                {
                    var job = Jobs[i];

                    try
                    {
                        var beginTime = DateTime.Now;
                        // Restorer doesn't expose Stop yet, so no cancellation source
                        // to plug in. Wire it up the day Restorer gets Pause/Stop.
                        // No-op progress callback: Restorer aggregates via its own
                        // restoredTotalBytes counter below.
                        long restored = job.Execute(CancellationToken.None, _ => { });
                        endTime = DateTime.Now;

                        var cryptoTime = TryDecrypt(job.SourceFile);

                        if (restored != job.FileSize)
                        {
                            /* Error : does nothing for now, should be handled later on */
                        }
                        restoredTotalBytes += restored;

                        float percent = TotalSize <= 0 ? 100f : Math.Clamp(((float)restoredTotalBytes / (float)TotalSize) * 100f, 0f, 100f);
                        Progress.SetProgress(percent);

                        _configManager.Logger.Log(
                            NewLogInfo(DateTime.Now, job.SourceFile, job.DestinationFile, job.FileSize, (endTime - beginTime).Milliseconds, cryptoTime)
                                .Format(_configManager.GetLogFormatConfig())
                        );
                        _configManager.State.Save(
                            NewStateInfo(
                                endTime,
                                Status.Active,
                                NewActiveStateInfo(Jobs.Count - i, this.TotalSize - restoredTotalBytes, job.SourceFile, job.DestinationFile)
                            )
                        );
                    }
                    catch (Exception ex)
                    {
                        _configManager.Logger.Log($"[RESTORE ERROR - File] {job.SourceFile}: {ex.Message}");
                        endTime = DateTime.Now;
                    }
                }
            }
            catch (Exception ex)
            {
                _configManager.Logger.Log($"[RESTORE ERROR - General] {ex.Message}");
            }
            finally
            {
                _configManager.State.Save(NewStateInfo(endTime, Status.Inactive, null));
            }
        }

        private int TryDecrypt(string restoredFile)
        {
            var extensions = _configManager.GetEncryptionExtensions();
            if (extensions.Count == 0) return 0;
            if (!Crypter.ShouldEncrypt(restoredFile, extensions)) return 0;
            return Crypter.DecryptFile(restoredFile, _configManager.GetEncryptionKey(), _configManager.GetCryptoSoftPath());
        }
    }
}
