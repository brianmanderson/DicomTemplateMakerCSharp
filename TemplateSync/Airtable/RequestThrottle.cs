using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using TemplateSync.Infrastructure;

namespace TemplateSync.Airtable
{
    /// <summary>
    /// Spaces request starts per base so the process never exceeds Airtable's
    /// 5 requests/second/base limit, even when several windows talk to the same base.
    /// </summary>
    public sealed class RequestThrottle
    {
        /// <summary>Process-wide throttle. 220 ms spacing keeps a margin under 5 requests/second.</summary>
        public static readonly RequestThrottle Shared = new RequestThrottle(TimeSpan.FromMilliseconds(220));

        private readonly ConcurrentDictionary<string, Gate> _gates = new ConcurrentDictionary<string, Gate>(StringComparer.Ordinal);

        public RequestThrottle(TimeSpan minimumInterval)
        {
            if (minimumInterval < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumInterval));
            }

            MinimumInterval = minimumInterval;
        }

        public TimeSpan MinimumInterval { get; }

        public async Task WaitAsync(string baseId, IClock clock, CancellationToken cancellationToken)
        {
            Gate gate = _gates.GetOrAdd(baseId, _ => new Gate());
            await gate.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                TimeSpan wait = gate.NextAllowedStart - clock.UtcNow;
                if (wait > TimeSpan.Zero)
                {
                    await clock.Delay(wait, cancellationToken).ConfigureAwait(false);
                }

                gate.NextAllowedStart = clock.UtcNow + MinimumInterval;
            }
            finally
            {
                gate.Semaphore.Release();
            }
        }

        private sealed class Gate
        {
            public readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);

            public DateTimeOffset NextAllowedStart = DateTimeOffset.MinValue;
        }
    }
}
