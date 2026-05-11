using Observer;

namespace SaveInterrupt
{
    // Publisher for pause/resume signals. Pause() and Resume() both fire
    // Notify(); subscribers read IsPaused on Update() to know which side of
    // the toggle they're on. Tracking the state on the Pauser itself keeps
    // every subscribed Saver in sync, even if they were attached at different
    // times — there's no toggle drift.
    public class Pauser : IPublisher
    {
        private readonly List<ISubscriber> _subscribers = new List<ISubscriber>();

        public bool IsPaused { get; private set; }

        public void Subscribe(ISubscriber subscriber)
        {
            if (!_subscribers.Contains(subscriber)) _subscribers.Add(subscriber);
        }

        public void Unsubscribe(ISubscriber subscriber)
        {
            if (_subscribers.Contains(subscriber)) _subscribers.Remove(subscriber);
        }

        public void Notify()
        {
            foreach (var s in _subscribers.ToArray()) s.Update();
        }

        public void Pause()
        {
            IsPaused = true;
            Notify();
        }

        public void Resume()
        {
            IsPaused = false;
            Notify();
        }
    }

    // Publisher for the stop signal. One-shot — once a Stopper fires it,
    // every subscribed Saver cancels and breaks out of its loop.
    public class Stopper : IPublisher
    {
        private readonly List<ISubscriber> _subscribers = new List<ISubscriber>();

        public void Subscribe(ISubscriber subscriber)
        {
            if (!_subscribers.Contains(subscriber)) _subscribers.Add(subscriber);
        }

        public void Unsubscribe(ISubscriber subscriber)
        {
            if (_subscribers.Contains(subscriber)) _subscribers.Remove(subscriber);
        }

        public void Notify()
        {
            foreach (var s in _subscribers.ToArray()) s.Update();
        }

        public void Stop() => Notify();
    }
}
