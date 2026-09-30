using Microsoft.Extensions.Logging;

namespace DicomTemplateMaker.Presentation.Tests.Fakes;

/// <summary>Keeps every log entry (level and formatted message).</summary>
public sealed class CapturingLogger : ILogger
{
    private readonly object gate = new();
    private readonly List<(LogLevel Level, string Message)> entries = new();

    public IReadOnlyList<(LogLevel Level, string Message)> Entries
    {
        get
        {
            lock (gate)
            {
                return entries.ToList();
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (gate)
        {
            entries.Add((logLevel, formatter(state, exception)));
        }
    }
}

/// <summary>A logger factory whose loggers all write to one <see cref="CapturingLogger"/>, and which records its disposal.</summary>
public sealed class CapturingLoggerFactory : ILoggerFactory
{
    public CapturingLogger Logger { get; } = new();

    public List<string> Categories { get; } = new();

    public bool Disposed { get; private set; }

    public void AddProvider(ILoggerProvider provider) => throw new NotSupportedException();

    public ILogger CreateLogger(string categoryName)
    {
        Categories.Add(categoryName);
        return Logger;
    }

    public void Dispose() => Disposed = true;
}
