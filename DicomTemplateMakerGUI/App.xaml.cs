using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DicomTemplateMakerGUI.Services;
using DicomTemplateMakerGUI.Shell;
using Microsoft.Extensions.Logging;
using Serilog;

namespace DicomTemplateMakerGUI
{
    /// <summary>
    /// Starts the program: logging, global exception handlers, the window settings and the template folder, then the
    /// main window.
    /// </summary>
    public partial class App : Application
    {
        private const string ProductName = "DICOM RT Template Maker";
        private const long LogFileSizeLimitBytes = 10L * 1024 * 1024;
        private const int RetainedLogFiles = 14;

        private readonly ILogger<App> logger = AppLog.For<App>();

        /// <summary>The program version, for the log and the About window.</summary>
        public static string Version
        {
            get
            {
                Assembly assembly = typeof(App).Assembly;
                string? informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                return informational ?? assembly.GetName().Version?.ToString() ?? "unknown";
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            string? logProblem = StartLogging();
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            try
            {
                UiSettingsStore settings = UiSettingsStore.ForCurrentUser(AppLog.For<UiSettingsStore>());
                settings.Load();
                TemplateRootResolution root = TemplateRootResolver.Resolve(settings.Settings.TemplateRoot, Environment.CurrentDirectory, AppPaths.ProgramDirectory);
                if (root.ShouldSave)
                {
                    settings.SetTemplateRoot(root.Root);
                }

                LogStartup(root, settings);
                FileDialogs.Configure(settings);
                var window = new MainWindow(settings, root, logProblem);
                MainWindow = window;
                window.Show();
            }
            catch (Exception ex)
            {
                // Startup boundary: without a main window the program would keep running invisibly.
                logger.LogCritical(ex, "The program could not start.");
                MessageBox.Show(ShellMessages.UnexpectedError(ex, AppPaths.LogsDirectory), ProductName + " could not start", MessageBoxButton.OK, MessageBoxImage.Error);
                AppLog.Shutdown();
                Shutdown(1);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            logger.LogInformation("{Product} exits (code {ExitCode}).", ProductName, e.ApplicationExitCode);
            AppLog.Shutdown();
            base.OnExit(e);
        }

        /// <summary>Sets up the daily rolling log file; returns why logging is off, or null.</summary>
        private static string? StartLogging()
        {
            string folder = AppPaths.LogsDirectory;
            try
            {
                Directory.CreateDirectory(folder);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return $"The log folder {folder} could not be created ({ex.Message}), so nothing is logged in this session.";
            }

            Serilog.Core.Logger serilog = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    Path.Combine(folder, "app-.log"),
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                    fileSizeLimitBytes: LogFileSizeLimitBytes,
                    shared: true,
                    rollingInterval: RollingInterval.Day,
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: RetainedLogFiles)
                .CreateLogger();
            ILoggerFactory factory = LoggerFactory.Create(builder => builder
                .SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information)
                .AddSerilog(serilog, dispose: true));
            AppLog.Initialize(factory);
            return null;
        }

        private void LogStartup(TemplateRootResolution root, UiSettingsStore settings)
        {
            logger.LogInformation(
                "{Product} {Version} starting on {OS} ({Framework}, {Architecture}). Program folder: {ProgramFolder}. Working directory: {WorkingDirectory}.",
                ProductName,
                Version,
                RuntimeInformation.OSDescription,
                RuntimeInformation.FrameworkDescription,
                RuntimeInformation.ProcessArchitecture,
                AppPaths.ProgramDirectory,
                Environment.CurrentDirectory);
            logger.LogInformation("Template folder: {TemplateRoot} ({Source}{Saved}). Window settings: {SettingsFile}.",
                root.Root, root.Describe(), root.ShouldSave ? ", now saved" : string.Empty, settings.FilePath);
            if (root.Note != null)
            {
                logger.LogWarning("{Note}", root.Note);
            }
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            logger.LogError(e.Exception, "Unhandled exception on the UI thread.");
            Window? owner = MainWindow != null && MainWindow.IsLoaded ? MainWindow : null;
            string message = ShellMessages.UnexpectedError(e.Exception, AppPaths.LogsDirectory);
            if (owner != null)
            {
                MessageBox.Show(owner, message, ProductName, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                MessageBox.Show(message, ProductName, MessageBoxButton.OK, MessageBoxImage.Error);
            }

            e.Handled = true;
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            logger.LogCritical(e.ExceptionObject as Exception, "Unhandled exception (the process is terminating: {IsTerminating}): {Exception}", e.IsTerminating, e.ExceptionObject);
            if (e.IsTerminating)
            {
                AppLog.Shutdown();
            }
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            logger.LogError(e.Exception, "A background task failed and nobody observed the failure.");
            e.SetObserved();
        }
    }
}
