using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Save
{
    public class SaveProgress
    {
    }

    public class Saver
    {
        private List<SaveJob> _jobs;

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
                job.Execute();
            }
        }

        public IReadOnlyList<SaveJob> GetJobs()
        {
            return _jobs.AsReadOnly();
        }
    }
}
