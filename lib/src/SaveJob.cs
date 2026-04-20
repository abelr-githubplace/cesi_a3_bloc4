namespace EasySaveLibrary.Save
{
    public abstract class SaveJob
    {
        protected Saver _saver;

        protected SaveJob(string name, string source, string destination)
        {
        }

        public void Execute()
        {
            var files = _saver.GetFilesToCopy();
            long totalSize = files.Sum(f => f.Length);
            _saver.Start();
        }
    }

    public class CompleteSaveJob : SaveJob
    {
        public CompleteSaveJob(string name, string source, string destination) 
            : base(name, source, destination)
        {
            _saver = new CompleteSaver(name, source, destination);
        }
    }

    public class DifferentialSaveJob : SaveJob
    {
        public DifferentialSaveJob(string name, string source, string destination) 
            : base(name, source, destination)
        {
            _saver = new DifferentialSaver(name, source, destination);
        }
    }
}