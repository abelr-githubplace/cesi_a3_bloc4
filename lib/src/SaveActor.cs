using Job;
using Config;
using SaveManager;
using State;
using EasyLog;

namespace Actor
{
    public abstract class SaveActor(SaveInfo save, SaveManager.Action saveAction, Progress.Progress progress)
    {
        public Guid Id { get; } = save.SaveId;
        public string Name { get; } = save.SaveName;
        public string SourcePath { get; } = save.SourcePath;
        public string DestinationPath { get; } = save.DestinationPath;
        public SaveManager.Action SaveAction { get; } = saveAction;
        public long TotalSize { get; init; } = 0;
        public Dictionary<string, long> FilesWithSizes { get; } = [];
        public Progress.Progress Progress { get; } = progress;
        protected readonly List<FileJob> Jobs = [];

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

        protected ActiveStateInfo NewActiveStateInfo(int jobsRemaining, long sizeRemaining, string source, string destination)
        {
            return new ActiveStateInfo
            {
                TotalFiles = this.FilesWithSizes.Count,
                TotalSize = this.TotalSize,
                FilesRemaining = jobsRemaining,
                SizeRemaining = sizeRemaining,
                Progress = this.Progress.GetProgress(),
                CurrentSourceFile = source,
                CurrentTargetFile = destination,
            };
        }
    }
}
