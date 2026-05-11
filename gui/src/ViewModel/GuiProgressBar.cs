using Observer;
using System.Windows;
using System.Windows.Threading;

namespace EasySave.GUI.ViewModels
{
    public class GuiProgressBar : ISubscriber
    {
        private SaveJob _job;
        private Saver.Progress _progressTracker;

        public GuiProgressBar(SaveJob job, Saver.Progress progressTracker)
        {
            _job = job;
            _progressTracker = progressTracker;
            _progressTracker.Subscribe(this);
        }

        // The Saver runs on a worker thread (Task.Run in Main.RunJob), so
        // Update() is invoked off the UI thread. Pushing _job.Progress directly
        // would fire INotifyPropertyChanged on the wrong thread — WPF queues
        // bindings on the dispatcher and intermediate values get coalesced /
        // dropped, making the ProgressBar look like it jumps 0 → 100.
        // Dispatcher.Invoke marshals the write to the UI thread so every
        // intermediate value is rendered.
        public void Update()
        {
            float value = _progressTracker.GetProgress();
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                _job.Progress = value;
            else
                dispatcher.Invoke(DispatcherPriority.Background, new System.Action(() => _job.Progress = value));
        }
    }
}