using Observer;

namespace Progress
{
    public class Progress : IPublisher
    {
        private readonly object _rwLock = new();
        private readonly List<ISubscriber> _subscribers;
        private float _progress;

        public Progress()
        {
            _progress = 0;
            _subscribers = [];
        }

        public void Subscribe(ISubscriber subscriber)
        {
            lock (_rwLock) { if (!_subscribers.Contains(subscriber)) _subscribers.Add(subscriber); }
        }

        public void Unsubscribe(ISubscriber subscriber)
        {
            lock (_rwLock) { _subscribers.Remove(subscriber); }
        }

        public float GetProgress()
        {
            lock (_rwLock) { return _progress; }
        }

        public void SetProgress(float progress)
        {
            lock (_rwLock) { _progress = progress; }
            Notify();
        }

        public void Notify()
        {
            lock (_rwLock) { foreach (var subscriber in _subscribers) subscriber.Update(); }
        }
    }
}
