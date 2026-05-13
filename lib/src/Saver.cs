using SaveManager;
using Job;
using Actor;
using State;
using BusinessSoftware;
using Config;

namespace Save
{
    internal enum SaveType { Complete, Differential }

    public class Saver : SaveActor
    {
        public Saver(SaveInfo save, SaveManager.Action saveAction, Progress.Progress progress)
            : base(save, saveAction, progress)
        {
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
                    FileJob? job = CreateJob(
                        file, destFile, fileSize, saveAction == SaveManager.Action.DifferentialSave ? SaveType.Differential : SaveType.Complete
                    );
                    if (job is not null) Jobs.Add(job);

                    totalSize += fileSize;
                }
            }
            TotalSize = totalSize;
        }

        private static FileJob? CreateJob(string sourceFile, string destFile, long fileSize, SaveType saveType)
        {
            var default_priority = Priority.Low;
            return saveType == SaveType.Differential
                ? new DifferentialSaveFileJob(sourceFile, destFile, destFile + ".diff", fileSize, default_priority)
                : new CompleteSaveFileJob(sourceFile, destFile, fileSize, default_priority);
        }

        public void Start()
        {
            long copiedTotalBytes = 0;
            var endTime = DateTime.Now;

            for (int i = 0; i < Jobs.Count; i++)
            {
                BusinessSoftwareMonitor.BusinessRunningEvent.Wait();

                var job = Jobs[i];

                var beginTime = DateTime.Now;
                long copiedSize = job.Execute();
                endTime = DateTime.Now;
                
                var cryptoTime = 0; // TODO: add crypto

                if (copiedSize != job.FileSize) /* Error : does nothing for now, should be handled later on */;
                copiedTotalBytes += copiedSize;

                float percent = TotalSize <= 0 ? 100f : Math.Clamp(((float)copiedTotalBytes / (float)TotalSize) * 100f, 0f, 100f);
                Progress.SetProgress(percent);

                ConfigManager.Get().Logger.Log(
                    NewLogInfo(DateTime.Now, job.SourceFile, job.DestinationFile, job.FileSize, (endTime - beginTime).Milliseconds, cryptoTime)
                        .Format(ConfigManager.Get().GetLogFormatConfig())
                );
                ConfigManager.Get().State.Save(
                    NewStateInfo(
                        endTime,
                        Status.Active,
                        NewActiveStateInfo(Jobs.Count - i, this.TotalSize - copiedTotalBytes, job.SourceFile, job.DestinationFile)
                    )
                );
            }
            ConfigManager.Get().State.Save(NewStateInfo(endTime, Status.Inactive, null));
        }
    }
}
