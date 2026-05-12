using Observer;

namespace EasySave.GUI.ViewModels
{
    public class GuiProgressBar : ISubscriber
    {
        private SaveJob _job;
        private Progress.Progress _progressTracker;

        public GuiProgressBar(SaveJob job, Progress.Progress progressTracker)
        {
            _job = job;
            _progressTracker = progressTracker;
            _progressTracker.Subscribe(this);
        }

        public void Update()
        {
            _job.Progress = _progressTracker.GetProgress();
        }
    }
}