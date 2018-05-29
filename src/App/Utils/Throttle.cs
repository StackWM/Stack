namespace LostTech.Stack.Utils {
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;

    public sealed class Throttle
    {
        static readonly Stopwatch stopwatch = Stopwatch.StartNew();
        TimeSpan lastStart = TimeSpan.Zero;

        public TimeSpan MinimumDelay { get; set; } = TimeSpan.FromMilliseconds(5);
        public async Task<bool> TryAcquire() {
            TimeSpan newStart = stopwatch.Elapsed;
            TimeSpan baseLastStart = this.lastStart;
            TimeSpan sinceLastStart = newStart - baseLastStart;

            if (sinceLastStart >= this.MinimumDelay) {
                this.lastStart = newStart;
                return true;
            }

            await Task.Delay(this.MinimumDelay - sinceLastStart).ConfigureAwait(false);

            if (this.lastStart != baseLastStart)
                return false;

            this.lastStart = newStart;
            return true;
        }
    }
}
