using Job;
using Config;
using SaveManager;
using State;
using EasyLog;

namespace Actor
{
    public abstract class SaveActor
    {
        public Guid Id { get; }
        public string Name { get; }
        public string SourcePath { get; }
        public string DestinationPath { get; }
        public SaveManager.Action SaveAction { get; }
        public long TotalSize { get; init; }
        public Dictionary<string, long> FilesWithSizes { get; }
        public Progress.Progress Progress { get; }
        protected readonly List<FileJob> Jobs;
        protected readonly ConfigManager _configManager;

        protected SaveActor(SaveInfo save, SaveManager.Action saveAction, Progress.Progress progress, ConfigManager configManager)
        {
            Id = save.SaveId;
            Name = save.SaveName;
            SourcePath = save.SourcePath;
            DestinationPath = save.DestinationPath;
            SaveAction = saveAction;
            Progress = progress;
            _configManager = configManager;
            Jobs = [];
            FilesWithSizes = [];
        }

        protected static bool IsFile(string path)
        {
            FileAttributes attr = File.GetAttributes(path);
            if (!attr.HasFlag(FileAttributes.Directory)) return true;
            return false;
        }

        protected static IEnumerable<string> GetAllFilesFullName(string path)
        {
            if (IsFile(path))
            {
                List<string> list = [path];
                return list;
            }
            return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories);
        }

        protected LogInfo NewLogInfo(DateTime date, string source, string destination, long fileSize, int time, int encryptionTime)
        {
            return new LogInfo {
                SaveName = Name,
                Action = SaveAction.ToString(),
                DateTime = date,
                SourceFile = source,
                DestinationFile = destination,
                FileSize = fileSize,
                TransferTime = time,
                EncryptionTime = encryptionTime,
            };
        }

        protected SaveState NewStateInfo(DateTime date, Status status, ActiveStateInfo? infos)
        {
            return new SaveState
            {
                Id = this.Id,
                Name = this.Name,
                SourcePath = this.SourcePath,
                DestinationPath = this.DestinationPath,
                LastActionTime = date,
                Status = status,
                ActiveStateInfo = infos,
            };
        }

        protected ActiveStateInfo NewActiveStateInfo(int jobsRemaining, long sizeRemaining, List<ActiveFileInfo> currentFiles)
        {
            return new ActiveStateInfo
            {
                TotalFiles = this.FilesWithSizes.Count,
                TotalSize = this.TotalSize,
                FilesRemaining = jobsRemaining,
                SizeRemaining = sizeRemaining,
                Progress = this.Progress.GetProgress(),
                CurrentFiles = currentFiles,
            };
        }

        // Convenience overload for callers that have a single in-flight file
        // (e.g. the sequential Restorer). Wraps it in a one-element list.
        protected ActiveStateInfo NewActiveStateInfo(int jobsRemaining, long sizeRemaining, string source, string destination)
        {
            return NewActiveStateInfo(jobsRemaining, sizeRemaining,
                new List<ActiveFileInfo> { new ActiveFileInfo { Source = source, Target = destination } });
        }
    }
}
