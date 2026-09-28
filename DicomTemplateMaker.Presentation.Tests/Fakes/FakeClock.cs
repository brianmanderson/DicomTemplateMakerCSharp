using TemplateSync.Infrastructure;

namespace DicomTemplateMaker.Presentation.Tests.Fakes;

/// <summary>Fixed time; the view models only read it.</summary>
public sealed class FakeClock : IClock
{
    public FakeClock(DateTimeOffset now) => UtcNow = now;

    public DateTimeOffset UtcNow { get; set; }

    public Task Delay(TimeSpan delay, CancellationToken cancellationToken) => throw new NotSupportedException("The view models never wait on the clock.");
}
