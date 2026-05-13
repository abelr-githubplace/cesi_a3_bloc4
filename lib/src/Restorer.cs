using Actor;
using Config;
using Job;
using SaveManager;
using State;

namespace Restore
{
    public sealed class Restorer : SaveActor
    {
        public Restorer(SaveInfo save, SaveManager.Action saveAction, Progress.Progress progress)
            : base(save, saveAction, progress)
        {
            long totalSize = 0;
            if (File.Exists(DestinationPath) || Directory.Exists(DestinationPath))
            {
                foreach (string file in GetAllFilesFullName(DestinationPath).Where(f => !f.EndsWith(".diff", StringComparison.Ordinal)))
                {
                    // Populate File to FileSize hashmap
                    long fileSize = new FileInfo(file).Length;
                    FilesWithSizes[file] = fileSize;

                    // Create save jobs
                    string relativePath = File.Exists(DestinationPath) ? Path.GetFileName(file) : Path.GetRelativePath(DestinationPath, file);
                    string sourceFile = Path.Combine(SourcePath, relativePath);
                    FileJob job = CreateJob(sourceFile, file, fileSize);
                    Jobs.Add(job);
                    totalSize += fileSize;
                }
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

            for (int i = 0; i < Jobs.Count; i++)
            {
                var job = Jobs[i];

                var beginTime = DateTime.Now;
                long restored = job.Execute();
                endTime = DateTime.Now;

                var cryptoTime = 0; // TODO: add crypto

                if (restored != job.FileSize) /* Error : does nothing for now, should be handled later on */;
                restoredTotalBytes += restored;

                float percent = TotalSize <= 0 ? 100f : Math.Clamp(((float)restoredTotalBytes / (float)TotalSize) * 100f, 0f, 100f);
                Progress.SetProgress(percent);

                ConfigManager.Get().Logger.Log(
                    NewLogInfo(DateTime.Now, job.SourceFile, job.DestinationFile, job.FileSize, (endTime - beginTime).Milliseconds, cryptoTime)
                        .Format(ConfigManager.Get().GetLogFormatConfig())
                );
                ConfigManager.Get().State.Save(
                    NewStateInfo(
                        endTime,
                        Status.Active,
                        NewActiveStateInfo(Jobs.Count - i, this.TotalSize - restoredTotalBytes, job.SourceFile, job.DestinationFile)
                    )
                );
            }
            ConfigManager.Get().State.Save(NewStateInfo(endTime, Status.Inactive, null));
        }
    }
}
