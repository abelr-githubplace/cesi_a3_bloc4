
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

        protected Saver(string name, string sourcePath, string destinationPath, Progress progress)
        {
            Name = name;
            SourcePath = sourcePath;
            DestinationPath = destinationPath;
            CurrentProgress = progress;

            _jobs = new List<SaveJob>();
            FilesWithSizes = new Dictionary<string, long>();
            TotalSize = 0;

            if (File.Exists(SourcePath) || Directory.Exists(SourcePath))
            {
                var files = GetAllFilesFullName(SourcePath).ToList();
                TotalSize = ComputeSaveSize(files);

                foreach (string file in files)
                {
                    FilesWithSizes[file] = new FileInfo(file).Length;
                }
            }
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

        private Int64 ComputeSaveSize(List<string> files)
        {
            Int64 size = 0;
            foreach (var file in files) 
            {
                size += new FileInfo(file).Length;
            }
            return size;
        }


        public void AddJob(SaveJob job)
        {
            _jobs.Add(job);
        }

        public void RemoveJob(SaveJob job)
        {
            _jobs.Remove(job);
        }

        public void ExecuteAll()
        {
            foreach (var job in _jobs)
            {
                job.Execute();
            }
        }

        public IReadOnlyList<SaveJob> GetJobs()
        {
            return _jobs.AsReadOnly();
        }

        public Progress GetProgress()
        {
            return CurrentProgress;
        }
    }
}
