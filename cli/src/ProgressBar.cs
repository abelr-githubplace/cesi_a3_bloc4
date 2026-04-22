using System;
using System.Diagnostics;
using Save;

namespace EasySave.CLI
{
    public class ProgressBar : ISubscriber
    {
        private float _progress;
        private Progress _observedProgress;

        public ProgressBar(Progress progress)
        {
            _observedProgress = progress;
            _progress = 0f;
        }

        public void Update()
        {
            float rawProgress = _observedProgress.GetProgress();
            _progress = Math.Max(0, Math.Min(rawProgress, 100));
            DrawProgressBar();
        }

        private void DrawProgressBar()
        {
            int totalBlocks = 20;
            int filledBlocks = (int)((_progress / 100) * totalBlocks);
            int emptyBlocks = totalBlocks - filledBlocks;

            string filled = new string('█', filledBlocks);
            string empty = new string('░', emptyBlocks);

            Console.Write($"\r [{filled}{empty}] {_progress:0.0}%  ");

            if (_progress >= 100f)
            {
                Console.WriteLine();
            }
        }
    }
}