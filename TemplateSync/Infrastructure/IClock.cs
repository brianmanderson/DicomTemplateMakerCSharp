using System;
using System.Threading;
using System.Threading.Tasks;

namespace TemplateSync.Infrastructure
{
    /// <summary>Abstracts time so throttling, retries and cache ages can be tested deterministically.</summary>
    public interface IClock
    {
        DateTimeOffset UtcNow { get; }

        Task Delay(TimeSpan delay, CancellationToken cancellationToken);
    }

    public sealed class SystemClock : IClock
    {
        public static readonly SystemClock Instance = new SystemClock();

        private SystemClock()
        {
        }

        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

        public Task Delay(TimeSpan delay, CancellationToken cancellationToken)
        {
            return delay <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(delay, cancellationToken);
        }
    }
}
