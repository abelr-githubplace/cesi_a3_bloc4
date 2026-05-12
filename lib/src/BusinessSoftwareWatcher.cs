using Config;

namespace BusinessSoftware
{
    // Singleton that polls the OS process list for any of the watched
    // "business software" processes and broadcasts state changes to every
    // subscriber. The poll task is reference-counted: it starts on the first
    // Subscribe() and stops when the last subscription is disposed, so we
    // don't burn a thread when no save is running.
    //
    // V3 semantics : when a watched process starts/stops, ALL active savers
    // must react in lockstep. Centralizing the detection here means one
    // poll instead of N (one per Saver), one source of truth, and exact
    // synchronization across jobs.
    public sealed class BusinessSoftwareWatcher : IDisposable
    {
        private static BusinessSoftwareWatcher? s_instance;
        private static readonly object s_initLock = new();

        private readonly ConfigManager _configManager;
        private readonly object _subLock = new();
        private readonly HashSet<Action<bool>> _subscribers = new();

        private CancellationTokenSource? _pollCts;
        private Task? _pollTask;
        private volatile bool _lastKnownRunning;

        // 500ms is a compromise between reactivity (user wants the pause to
        // kick in quickly when Calculator starts) and CPU load (each poll
        // walks the whole Windows process list).
        private static readonly TimeSpan s_pollPeriod = TimeSpan.FromMilliseconds(500);

        private BusinessSoftwareWatcher(ConfigManager configManager)
        {
            _configManager = configManager;
        }

        public static BusinessSoftwareWatcher Get(ConfigManager configManager)
        {
            if (s_instance == null)
            {
                lock (s_initLock)
                {
                    s_instance ??= new BusinessSoftwareWatcher(configManager);
                }
            }
            return s_instance;
        }

        // Snapshot of the latest poll result. May be slightly stale (up to
        // one poll period), which is fine for the cases that consult it
        // (e.g. GUI computing an initial label).
        public bool IsAnyRunning => _lastKnownRunning;

        // The callback is invoked immediately with the current state, then
        // again every time the state flips. The returned IDisposable removes
        // the subscription and stops the poll task if it was the last one.
        public IDisposable Subscribe(Action<bool> onChanged)
        {
            bool startPolling;
            lock (_subLock)
            {
                _subscribers.Add(onChanged);
                startPolling = _subscribers.Count == 1;
            }
            if (startPolling) StartPolling();

            // Fire once with the latest snapshot so the subscriber doesn't
            // have to query IsAnyRunning separately.
            SafeInvoke(onChanged, _lastKnownRunning);

            return new Subscription(this, onChanged);
        }

        private void Unsubscribe(Action<bool> onChanged)
        {
            bool stopPolling;
            lock (_subLock)
            {
                _subscribers.Remove(onChanged);
                stopPolling = _subscribers.Count == 0;
            }
            if (stopPolling) StopPolling();
        }

        private void StartPolling()
        {
            _pollCts = new CancellationTokenSource();
            var token = _pollCts.Token;
            _pollTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    var watched = _configManager.GetBusinessSoftwares();
                    // Empty watch list = nothing to detect. We still publish
                    // false so any subscriber currently parked on "true" gets
                    // released cleanly when the user clears the list.
                    bool nowRunning = watched.Count > 0
                        && BusinessSoftwareMonitor.IsAnyRunning(watched);

                    if (nowRunning != _lastKnownRunning)
                    {
                        _lastKnownRunning = nowRunning;
                        NotifyAll(nowRunning);
                    }

                    try { await Task.Delay(s_pollPeriod, token); }
                    catch (TaskCanceledException) { break; }
                }
            }, token);
        }

        private void StopPolling()
        {
            _pollCts?.Cancel();
            _pollCts = null;
            _pollTask = null;
            // Reset so a future subscriber doesn't observe a stale "true"
            // from a previous run; they'll get the fresh value on next poll.
            _lastKnownRunning = false;
        }

        private void NotifyAll(bool running)
        {
            Action<bool>[] snapshot;
            lock (_subLock) snapshot = _subscribers.ToArray();
            foreach (var s in snapshot) SafeInvoke(s, running);
        }

        // A misbehaving subscriber shouldn't break the dispatch loop for the
        // others. Swallow and move on.
        private static void SafeInvoke(Action<bool> cb, bool value)
        {
            try { cb(value); } catch { }
        }

        public void Dispose()
        {
            StopPolling();
            lock (_subLock) _subscribers.Clear();
        }

        private sealed class Subscription : IDisposable
        {
            private readonly BusinessSoftwareWatcher _owner;
            private readonly Action<bool> _cb;
            private bool _disposed;

            public Subscription(BusinessSoftwareWatcher owner, Action<bool> cb)
            {
                _owner = owner;
                _cb = cb;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _owner.Unsubscribe(_cb);
            }
        }
    }
}
