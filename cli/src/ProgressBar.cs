namespace EasySaveConsole
{
    public class ProgressBar : Observer.ISubscriber
    {
        private readonly int _position;
        private readonly string _saveName;
        private readonly Progress.Progress _progress;

        public ProgressBar(string saveName, int position, Progress.Progress progress)
        {
            _progress = progress;
            _position = position;
            _saveName = saveName;

            progress.Subscribe(this);
        }

        public void Update()
        {
            if (Console.IsOutputRedirected) return;

            try
            {
                float progress = _progress.GetProgress();
                int totalBlocks = Console.WindowWidth - _saveName.Length - 15;
                int filledBlocks = (int)((progress / 100) * totalBlocks);
                int emptyBlocks = totalBlocks - filledBlocks;

                string filled = new('█', filledBlocks);
                string empty = new('░', emptyBlocks);

                var previous_position = Console.GetCursorPosition();
                Console.SetCursorPosition(0, Console.CursorTop + Console.WindowHeight - previous_position.Top - _position - 1);
                Console.Write($"=> {_saveName} [{filled}{empty}] {progress,6:0.00}%");
                Console.SetCursorPosition(previous_position.Left, previous_position.Top);
            }
            catch { return; }
        }
    }
}