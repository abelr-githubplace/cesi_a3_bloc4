namespace Save
{
    // V3 constraint: no two files larger than the configured threshold may be
    // transferred at the same time, across the WHOLE process (every Saver
    // shares this gate). Files at or below the threshold pass freely so
    // workers stay busy with small files while one big transfer happens.
    //
    // The threshold is read per-acquisition so a config change is honored
    // by the next file. A 0/negative threshold means "no gating".
    public static class LargeFileGate
    {
        private static readonly SemaphoreSlim s_semaphore = new SemaphoreSlim(1, 1);

        // Use `using` at the call site:
        //   using var scope = LargeFileGate.AcquireIfLarge(size, threshold, ct);
        //   // copy here — released on scope.Dispose()
        //
        // Throws OperationCanceledException if the token fires while waiting
        // on the semaphore — caller catches and bails out of its work unit.
        public static IDisposable AcquireIfLarge(long fileSize, int thresholdKB, CancellationToken token)
        {
            if (thresholdKB <= 0) return NoOpScope.Instance;

            long thresholdBytes = (long)thresholdKB * 1024;
            // Strictly greater: a file exactly at threshold is NOT gated.
            if (fileSize <= thresholdBytes) return NoOpScope.Instance;

            s_semaphore.Wait(token);
            return new HeldScope();
        }

        private sealed class HeldScope : IDisposable
        {
            private int _released;
            public void Dispose()
            {
                // Guard against accidental double-Dispose() that would push
                // the semaphore above its max count.
                if (Interlocked.Exchange(ref _released, 1) == 0)
                    s_semaphore.Release();
            }
        }

        private sealed class NoOpScope : IDisposable
        {
            public static readonly NoOpScope Instance = new NoOpScope();
            public void Dispose() { }
        }
    }
}
