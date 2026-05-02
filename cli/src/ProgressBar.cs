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

            progress.Subscribe(this);
        }

        public void Update()
        {
            float progress = _progress.GetProgress();
            if (progress == 0f) return;

            int totalBlocks = Console.WindowWidth - _saveName.Length - 15;
            int filledBlocks = (int)((progress / 100) * totalBlocks);
            int emptyBlocks = totalBlocks - filledBlocks;

            string filled = new string('█', filledBlocks);
            string empty = new string('░', emptyBlocks);

            Console.Write($"=> {_saveName} [{filled}{empty}] {progress,6:0.00}%\r");
        }
    }
}