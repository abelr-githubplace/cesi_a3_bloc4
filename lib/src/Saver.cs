using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Save
{
    public interface ISubscriber
    {
        void Update();
    }

    public class Progress
    {
        private List<ISubscriber> _subscribers;
        protected float _progress;

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
            _subscribers.Remove(subscriber);
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

    public class Saver
    {
        private List<SaveJob> _jobs;
        private SaveJob _currentJob;

        public Saver()
        {
            _jobs = new List<SaveJob>();
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
                _currentJob = job;
                job.Execute();
            }
            _currentJob = null;
        }

        public IReadOnlyList<SaveJob> GetJobs()
        {
            return _jobs.AsReadOnly();
        }

        public float GetProgress()
        {
            return _currentJob?.GetProgress() ?? 0f;
        }
    }
}
