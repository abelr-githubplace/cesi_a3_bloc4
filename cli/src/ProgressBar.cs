namespace EasySaveConsole
{
    public class ProgressBar : Observer.ISubscriber
    {
        private int _position;
        private string _saveName;
        private Saver.Progress _progress;

        public ProgressBar(string saveName, int position, Saver.Progress progress)
        {
            _progress = progress;
            _position = position;
            _saveName = saveName;

            progress.Subscribe(this);
        }

        public void Update()
        {
            float progress = _progress.GetProgress();
            int totalBlocks = Console.WindowWidth - _saveName.Length - 15;
            int filledBlocks = (int)((progress / 100) * totalBlocks);
            int emptyBlocks = totalBlocks - filledBlocks;

            string filled = new string('█', filledBlocks);
            string empty = new string('░', emptyBlocks);

            var previous_position = Console.GetCursorPosition();
            Console.SetCursorPosition(0, Console.CursorTop + Console.WindowHeight - previous_position.Top - _position - 1);
            Console.Write($"=> {_saveName} [{filled}{empty}] {progress,6:0.00}%");
            Console.SetCursorPosition(previous_position.Left, previous_position.Top);
        }
    }
}