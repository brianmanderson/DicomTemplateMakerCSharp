using TemplateSync.Infrastructure;

namespace TemplateSync.Tests.Fakes;

/// <summary>Manual clock: Delay returns immediately and advances time, so retry waits cost no wall time.</summary>
public sealed class FakeClock : IClock
{
    public FakeClock(DateTimeOffset start) => UtcNow = start;

    public DateTimeOffset UtcNow { get; set; }

    public List<TimeSpan> Delays { get; } = new();

    public Task Delay(TimeSpan delay, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Delays.Add(delay);
        if (delay > TimeSpan.Zero)
        {
            UtcNow += delay;
        }

        return Task.CompletedTask;
    }

    public void Advance(TimeSpan by) => UtcNow += by;
}
