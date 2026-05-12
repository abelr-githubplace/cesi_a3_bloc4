namespace Save
{
    // V3: cross-job coordination for "priority" files. While any priority
    // file remains in the queue of any active Saver, no non-priority file
    // may start. Implementation : a single global pending counter; an
    // event that is Set when the counter reaches 0.
    //
    // Lifecycle per Saver:
    //   AddPending(N)              ⟵ Start() : N = nb of priority files
    //   MarkOneStarted()           ⟵ each iteration, when a priority file
    //                                begins copying
    //   MarkManyStarted(leftover)  ⟵ Start() finally : releases the count
    //                                for any priority file we never
    //                                reached (cancellation / exception)
    //
    // Non-priority workers gate themselves with WaitForAllPending(token).
    //
    // "MarkOneStarted" is fired BEFORE Execute(), not after, because the
    // cahier defines "en attente" as "not yet started" — once a priority
    // file is being copied, it no longer holds back the others.
    public static class PriorityGate
    {
        private static int s_pending;
        private static readonly ManualResetEventSlim s_allDone = new ManualResetEventSlim(true);

        public static void AddPending(int n)
        {
            if (n <= 0) return;
            int newCount = Interlocked.Add(ref s_pending, n);
            if (newCount > 0) s_allDone.Reset();
        }

        public static void MarkOneStarted()
        {
            int remaining = Interlocked.Decrement(ref s_pending);
            // <= 0 covers spurious double-decrements and keeps the gate open.
            if (remaining <= 0) s_allDone.Set();
        }

        public static void MarkManyStarted(int n)
        {
            if (n <= 0) return;
            int remaining = Interlocked.Add(ref s_pending, -n);
            if (remaining <= 0) s_allDone.Set();
        }

        public static void WaitForAllPending(CancellationToken token)
        {
            s_allDone.Wait(token);
        }
    }
}
