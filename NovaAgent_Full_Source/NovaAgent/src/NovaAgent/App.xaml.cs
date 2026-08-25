using System.Windows;
using NovaAgent.Services;

namespace NovaAgent;

public partial class App : System.Windows.Application
{
    private MainWindow? _window;
    private Mutex? _singleInstance;
    private EventWaitHandle? _activationEvent;
    private RegisteredWaitHandle? _activationRegistration;

    private const string InstanceMutexName = @"Local\NovaAgent.Desktop";
    private const string ActivationEventName = @"Local\NovaAgent.Desktop.Activate";

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstance = new Mutex(true, InstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            try
            {
                using var activation = EventWaitHandle.OpenExisting(ActivationEventName);
                activation.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                MessageBox.Show("Nova Agent is already running. Check the system tray.",
                    "Nova Agent", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            Shutdown();
            return;
        }

        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Error("Unhandled UI error.", args.Exception);
            MessageBox.Show("Nova Agent recovered from an unexpected error. Details were written to the log.",
                "Nova Agent", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLog.Error("Unhandled application error.", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Error("Unobserved background task error.", args.Exception);
            args.SetObserved();
        };

        StorageMaintenanceService.Run();
        AppLog.Info("Nova Agent started.");

        _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationEventName);
        _activationRegistration = ThreadPool.RegisterWaitForSingleObject(
            _activationEvent,
            (_, _) => Dispatcher.BeginInvoke(() => _window?.RestoreFromExternalActivation()),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);

        var safeMode = e.Args.Any(argument =>
            string.Equals(argument, "--safe-mode", StringComparison.OrdinalIgnoreCase));
        _window = new MainWindow(safeMode);
        MainWindow = _window;
        _window.Show();

        if (e.Args.Any(argument =>
                string.Equals(argument, "--minimized", StringComparison.OrdinalIgnoreCase)))
        {
            Dispatcher.BeginInvoke(_window.HideForStartup);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppLog.Info("Nova Agent stopped.");
        _activationRegistration?.Unregister(null);
        _activationEvent?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
