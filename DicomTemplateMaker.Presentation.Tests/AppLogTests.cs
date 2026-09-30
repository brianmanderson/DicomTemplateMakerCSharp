using DicomTemplateMaker.Presentation.Tests.Fakes;
using DicomTemplateMakerGUI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DicomTemplateMaker.Presentation.Tests;

/// <summary>AppLog is static, so all of its tests live in this one class (tests in a class never run in parallel).</summary>
public sealed class AppLogTests : IDisposable
{
    public void Dispose() => AppLog.Shutdown();

    [Fact]
    public void Before_startup_loggers_do_nothing()
    {
        AppLog.Shutdown();
        ILogger<AppLogTests> logger = AppLog.For<AppLogTests>();

        Assert.Same(NullLoggerFactory.Instance, AppLog.Factory);
        Assert.False(logger.IsEnabled(LogLevel.Critical));
        logger.LogError("nobody hears this");
    }

    [Fact]
    public void A_logger_taken_before_startup_writes_once_the_factory_is_set()
    {
        AppLog.Shutdown();
        ILogger<AppLogTests> logger = AppLog.For<AppLogTests>();
        var factory = new CapturingLoggerFactory();

        AppLog.Initialize(factory);
        logger.LogInformation("hello {Name}", "log");

        Assert.Equal(new[] { typeof(AppLogTests).FullName }, factory.Categories);
        Assert.Equal((LogLevel.Information, "hello log"), Assert.Single(factory.Logger.Entries));
    }

    [Fact]
    public void Shutdown_disposes_the_factory_once_and_returns_to_no_op_loggers()
    {
        var factory = new CapturingLoggerFactory();
        AppLog.Initialize(factory);
        ILogger logger = AppLog.For("Test");

        AppLog.Shutdown();
        AppLog.Shutdown();
        logger.LogWarning("after shutdown");

        Assert.True(factory.Disposed);
        Assert.Empty(factory.Logger.Entries);
        Assert.Same(NullLoggerFactory.Instance, AppLog.Factory);
    }

    [Fact]
    public void Initializing_again_replaces_and_disposes_the_previous_factory()
    {
        var first = new CapturingLoggerFactory();
        var second = new CapturingLoggerFactory();
        ILogger logger = AppLog.For("Test");
        AppLog.Initialize(first);
        logger.LogInformation("one");

        AppLog.Initialize(second);
        logger.LogInformation("two");

        Assert.True(first.Disposed);
        Assert.Equal("one", Assert.Single(first.Logger.Entries).Message);
        Assert.Equal("two", Assert.Single(second.Logger.Entries).Message);
    }
}
