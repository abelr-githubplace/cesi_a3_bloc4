namespace EasySaveLibrary.Save
{
    public class SaveJob
    {
        private readonly Saver _saver;

        public SaveJob(string name, string source, string destination, SaveType type)
        {
            _saver = type == SaveType.Complete
                ? new CompleteSaver(name, source, destination)
                : new DifferentialSaver(name, source, destination);
        }

        public void Execute()
        {
            var files = _saver.GetFilesToCopy();
            long totalSize = files.Sum(f => f.Length);
            _saver.Start();
        }
    }
}