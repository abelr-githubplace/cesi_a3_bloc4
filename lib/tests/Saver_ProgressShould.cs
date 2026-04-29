using Microsoft.VisualStudio.TestTools.UnitTesting;
using Saver;
using Observer;

namespace EasySaveLibrary.Tests
{
    [TestClass]
    public class Saver_ProgressShould
    {
        private sealed class CountingSubscriber : ISubscriber
        {
            public int Updates { get; private set; }
            public void Update() => Updates++;
        }

        [TestMethod]
        public void NewProgress_StartsAtZero()
        {
            var progress = new Progress();
            Assert.AreEqual(0f, progress.GetProgress());
        }

        [TestMethod]
        public void SetProgress_UpdatesValueAndNotifiesSubscribers()
        {
            var progress = new Progress();
            var sub = new CountingSubscriber();
            progress.Subscribe(sub);

            progress.SetProgress(42.5f);

            Assert.AreEqual(42.5f, progress.GetProgress());
            Assert.AreEqual(1, sub.Updates);
        }

        [TestMethod]
        public void Subscribe_DoesNotAddSameSubscriberTwice()
        {
            var progress = new Progress();
            var sub = new CountingSubscriber();

            progress.Subscribe(sub);
            progress.Subscribe(sub);
            progress.SetProgress(10f);

            Assert.AreEqual(1, sub.Updates, "A duplicate Subscribe must be a no-op");
        }

        [TestMethod]
        public void Unsubscribe_StopsNotifications()
        {
            var progress = new Progress();
            var sub = new CountingSubscriber();
            progress.Subscribe(sub);
            progress.Unsubscribe(sub);

            progress.SetProgress(50f);

            Assert.AreEqual(0, sub.Updates);
        }

        [TestMethod]
        public void Notify_DispatchesToEverySubscriber()
        {
            var progress = new Progress();
            var a = new CountingSubscriber();
            var b = new CountingSubscriber();
            progress.Subscribe(a);
            progress.Subscribe(b);

            progress.Notify();

            Assert.AreEqual(1, a.Updates);
            Assert.AreEqual(1, b.Updates);
        }
    }
}
