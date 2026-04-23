using System;

namespace Observer
{
    public interface IPublisher
    {
        public void Subscribe(ISubscriber subscriber);
        public void Unsubscribe(ISubscriber subscriber);
        public void Notify();
    }

    public interface ISubscriber
    {
        public void Update();
    }
}
