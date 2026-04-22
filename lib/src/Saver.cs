using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Save
{
    public class Progress 
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
            if (!_subscribers.Contains(subscriber))
            {
                _subscribers.Add(subscriber);
            }
        }

        public void Unsubscribe(ISubscriber subscriber)
        {
            if (_subscribers.Contains(subscriber))
            {
                _subscribers.Remove(subscriber);
            }
        }

        public void Notify()
        {
            foreach (var subscriber in _subscribers)
            {
                subscriber.Update();
            }
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

    public abstract class Saver
    {
        protected List<SaveJob> _jobs;

        public Progress CurrentProgress { get; protected set; }

        public string Name { get; protected set; }
        public string SourcePath { get; protected set; }
        public string DestinationPath { get; protected set; }
        public long TotalSize { get; protected set; }

        public Dictionary<string, long> FilesWithSizes { get; protected set; }

        protected Saver(SaveInfo save)
        {
            Name = save.SaveName;
            SourcePath = save.SourcePath;
            DestinationPath = save.DestinationPath;
            CurrentProgress = new Progress();

            _jobs = new List<SaveJob>();
            FilesWithSizes = new Dictionary<string, long>();
            TotalSize = 0;

            if (File.Exists(SourcePath) || Directory.Exists(SourcePath))
            {
                var files = GetAllFilesFullName(SourcePath).ToList();

                foreach (string file in files)
                {
                    long fileSize = GetFileSize(file);
                    FilesWithSizes[file] = fileSize;
                    TotalSize += fileSize;
                }
            }
        }

        private long GetFileSize(string path)
        {
            return new FileInfo(path).Length;
        }

        private bool IsFile(string path)
        {
            FileAttributes attr = File.GetAttributes(path);
            if (!attr.HasFlag(FileAttributes.Directory)) return true;
            return false;
        }

        private IEnumerable<string> GetAllFilesFullName(string path)
        {
            if (IsFile(path))
            {
                var list = new List<string>();
                list.Add(path);
                return list;
            }
            return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories);
        }

        public void AddJob(SaveJob job)
        {
            _jobs.Add(job);
        }

        public void RemoveJob(SaveJob job)
        {
            _jobs.Remove(job);
        }

        protected abstract SaveJob CreateJob(string sourceFile, string destFile, long fileSize);

        public void ExecuteAll()
        {
            long copiedTotalBytes = 0;
            if (_jobs.Count == 0)
            {
                foreach (var kvp in FilesWithSizes)
                {
                    string sourceFile = kvp.Key;
                    long fileSize = kvp.Value;
                    string relativePath = Path.GetRelativePath(SourcePath, sourceFile);
                    string destFile = Path.Combine(DestinationPath, relativePath);
                    AddJob(CreateJob(sourceFile, destFile, fileSize));
                }
            }

            foreach (var job in _jobs)
            {
                var beginTime = DateTime.Now;
                long copiedSize = job.Execute();
                var endTime = DateTime.Now;
                Logger.Log(new LogInfo { DateTime = DateTime.Now, SaveName = Name, SourceFile = job.SourceFile, DestinationFile = job.DestinationFile, FileSize = job.FileSize, TransferTime = (endTime - beginTime).Milliseconds });

                if (copiedSize != job.FileSize)
                {

                    TotalSize += (copiedSize - job.FileSize);
                }

                copiedTotalBytes += copiedSize;

                float percent = TotalSize == 0 ? 100f : ((float)copiedTotalBytes / TotalSize) * 100f;

                if (percent > 100f) percent = 100f;
                if (percent < 0f) percent = 0f;

                CurrentProgress.SetProgress(percent);
            }
        }

        public IReadOnlyList<SaveJob> GetJobs()
        {
            return _jobs.AsReadOnly();
        }

        public Progress GetProgress() Name, SourceFile = job.SourceFile, DestinationFile = job.DestinationFile, FileSize = job.FileSize, TransferTime = DateTime.Now
    });

                if (copiedSize != job.FileSize)
                {
                    throw new System.Exception($"Erreur de sauvegarde : {job.FileSize} octets attendus, mais {copiedSize} octets copiés.");
                }

copiedTotalBytes += copiedSize;

float percent = TotalSize == 0 ? 100f : ((float)copiedTotalBytes / TotalSize) * 100f;
CurrentProgress.SetProgress(percent);
            }
        }

        public IReadOnlyList<SaveJob> GetJobs()
{
            return CurrentProgress;
        }
    }

    public class CompleteSaver : Saver
    {
        public CompleteSaver(string name, string sourcePath, string destinationPath) 
            : base(name, sourcePath, destinationPath)
        {
        }

        protected override SaveJob CreateJob(string sourceFile, string destFile, long fileSize)
        {
            return new CompleteSaveJob(Name, sourceFile, destFile, fileSize);
        }
    }

    public class DifferentialSaver : Saver
    {
        public DifferentialSaver(string name, string sourcePath, string destinationPath) 
            : base(name, sourcePath, destinationPath)
        {
        }

        protected override SaveJob CreateJob(string sourceFile, string destFile, long fileSize)
        {
            return new DifferentialSaveJob(Name, sourceFile, destFile, fileSize);
        }
    }
}
