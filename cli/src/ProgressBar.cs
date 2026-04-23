namespace EasySaveConsole
{
    public class ProgressBar : Observer.ISubscriber
    {
        private string _saveName;
        private Saver.Progress _progress;

        public ProgressBar(string saveName, Saver.Progress progress)
        {
            _progress = progress;
            _saveName = saveName;
            _progress = progress;

            progress.Subscribe(this);
        }

        public void Update()
        {
            float progress = _progress.GetProgress();
            int totalBlocks = 20;
            int filledBlocks = (int)((progress / 100) * totalBlocks);
            int emptyBlocks = totalBlocks - filledBlocks;

            string filled = new string('█', filledBlocks);
            string empty = new string('░', emptyBlocks);

            Console.Write($"\r {_saveName} [{filled}{empty}] {progress:0.0}%  ");

            if (progress >= 100f) Console.WriteLine();
        }
    }
}