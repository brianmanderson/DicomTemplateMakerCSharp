using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DicomTemplateMakerGUI.Services
{
    /// <summary>
    /// The program's logger factory. Until <see cref="Initialize"/> runs (App.OnStartup in the GUI) every logger is a
    /// no-op, so windows, services and tests can log without any setup. A logger handed out before then starts
    /// writing to the real factory once it is set, so it is safe to keep one in a field.
    /// </summary>
    public static class AppLog
    {
        private static FactoryHolder current = new FactoryHolder(NullLoggerFactory.Instance);

        /// <summary>The factory set by <see cref="Initialize"/>, or a no-op factory before startup and after shutdown.</summary>
        public static ILoggerFactory Factory => Volatile.Read(ref current).Factory;

        /// <summary>A logger for <typeparamref name="T"/> that follows the current <see cref="Factory"/>.</summary>
        public static ILogger<T> For<T>()
        {
            return new ForwardingLogger<T>();
        }

        /// <summary>A logger for <paramref name="category"/> that follows the current <see cref="Factory"/>.</summary>
        public static ILogger For(string category)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(category);
            return new ForwardingLogger(category);
        }

        /// <summary>Makes <paramref name="loggerFactory"/> the program's factory; <see cref="Shutdown"/> disposes it.</summary>
        public static void Initialize(ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(loggerFactory);
            FactoryHolder previous = Interlocked.Exchange(ref current, new FactoryHolder(loggerFactory));
            DisposeUnlessNull(previous.Factory);
        }

        /// <summary>
        /// Flushes and disposes the factory set by <see cref="Initialize"/>; loggers are no-ops afterwards. Safe to
        /// call more than once (the process-exit and crash paths both call it).
        /// </summary>
        public static void Shutdown()
        {
            FactoryHolder previous = Interlocked.Exchange(ref current, new FactoryHolder(NullLoggerFactory.Instance));
            DisposeUnlessNull(previous.Factory);
        }

        private static void DisposeUnlessNull(ILoggerFactory factory)
        {
            if (!ReferenceEquals(factory, NullLoggerFactory.Instance))
            {
                factory.Dispose();
            }
        }

        /// <summary>One factory; a new holder per factory lets loggers notice the change with a reference compare.</summary>
        private sealed class FactoryHolder
        {
            public FactoryHolder(ILoggerFactory factory)
            {
                Factory = factory;
            }

            public ILoggerFactory Factory { get; }
        }

        private class ForwardingLogger : ILogger
        {
            private readonly string category;
            private (FactoryHolder Holder, ILogger Logger)? cached;

            public ForwardingLogger(string category)
            {
                this.category = category;
            }

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
            {
                return Inner().BeginScope(state);
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return Inner().IsEnabled(logLevel);
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                Inner().Log(logLevel, eventId, state, exception, formatter);
            }

            private ILogger Inner()
            {
                FactoryHolder holder = Volatile.Read(ref current);
                (FactoryHolder Holder, ILogger Logger)? known = cached;
                if (known.HasValue && ReferenceEquals(known.Value.Holder, holder))
                {
                    return known.Value.Logger;
                }

                ILogger logger = holder.Factory.CreateLogger(category);
                cached = (holder, logger);
                return logger;
            }
        }

        private sealed class ForwardingLogger<T> : ForwardingLogger, ILogger<T>
        {
            public ForwardingLogger()
                : base(CategoryOf(typeof(T)))
            {
            }

            private static string CategoryOf(Type type)
            {
                return (type.FullName ?? type.Name).Replace('+', '.');
            }
        }
    }
}
