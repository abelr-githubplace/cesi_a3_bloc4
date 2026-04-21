using System;

namespace EasySave.CLI
{
    public interface ISubscriber
    {
        void Update(float progress);
    }

    public class SaveProgressBar : ISubscriber
    {
        private float _progress;

        public void Update(float progress)
        {
            this._progress = Math.Max(0, Math.Min(progress, 100));
            DrawProgressBar();
        }

        private void DrawProgressBar()
        {
            int totalBlocks = 20;
            int filledBlocks = (int)((_progress / 100) * totalBlocks);
            int emptyBlocks = totalBlocks - filledBlocks;

            string filled = new string('█', filledBlocks);
            string empty = new string('░', emptyBlocks);

            Console.Write($"\rProgression: [{filled}{empty}] {_progress:0.0}%  ");
        }
    }
}