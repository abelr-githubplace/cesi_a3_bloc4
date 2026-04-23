using SaveManager;
using StateManager;
using Job;
using Observer;

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
        public uint Id { get; set; }
        public string Name { get; }
        public string SourcePath { get; }
        public string DestinationPath { get; }
        public long TotalSize { get; }
        public Dictionary<string, long> FilesWithSizes { get; }
        public Progress Progress { get; }

        protected List<SaveJob> Jobs;
        private Config _config;

        public Saver(SaveInfo save, SaveType saveType, Progress progress, Config config)
        {
            Id = save.SaveId;
            Name = save.SaveName;
            SourcePath = save.SourcePath;
            DestinationPath = save.DestinationPath;
            Progress = progress;
            _config = config;

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
                    if (job == null) /* Error : should be handled */;
                    Jobs.Add(job);
                    
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
                var job = Jobs[i];

                var beginTime = DateTime.Now;
                long copiedSize = job.Execute();
                endTime = DateTime.Now;

                if (copiedSize != job.FileSize) /* Error : does nothing for now, should be handled later on */;
                copiedTotalBytes += copiedSize;

                float percent = TotalSize <= 0 ? 100f : Math.Clamp(((float)copiedTotalBytes / (float)TotalSize) * 100f, 0f, 100f);
                Progress.SetProgress(percent);

                _config.Logger.Log(
                    new EasyLog.LogInfo {
                        DateTime = DateTime.Now,
                        SaveName = Name,
                        SourceFile = job.SourceFile,
                        DestinationFile = job.DestinationFile,
                        FileSize = job.FileSize,
                        TransferTime = (endTime - beginTime).Milliseconds
                    }
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
    }
}
