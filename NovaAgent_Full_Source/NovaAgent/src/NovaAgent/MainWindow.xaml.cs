using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NovaAgent.Models;
using NovaAgent.Services;

namespace NovaAgent;

public partial class MainWindow : Window
{
    private sealed record MicrophoneOption(int Number, string Name);

    private readonly SettingsService _settingsService = new();
    private readonly HistoryService _history = new();
    private readonly SpeechOutputService _speech = new();
    private readonly AudioCaptureService _audio = new();
    private readonly WindowsControlService _windows = new();
    private readonly FileSearchService _files = new();
    private readonly AutoStartService _autoStart = new();
    private readonly DiagnosticsService _diagnostics;
    private readonly WhisperService _whisper;
    private readonly CommandProcessor _commands;
    private readonly VoiceLoopService _voice;
    private readonly System.Windows.Forms.NotifyIcon _tray;
    private readonly bool _safeMode;
    private bool _exitRequested;

    public MainWindow(bool safeMode = false)
    {
        InitializeComponent();
        _safeMode = safeMode;

        _diagnostics = new DiagnosticsService(_settingsService);
        _whisper = new WhisperService(_settingsService);
        _commands = new CommandProcessor(_windows, _files, _settingsService);
        _voice = new VoiceLoopService(
            _settingsService, _audio, _whisper, _speech, _commands, _history);

        _voice.StatusChanged += message => Dispatcher.Invoke(() => StatusText.Text = message);
        _voice.TranscriptReceived += message => Dispatcher.Invoke(() => TranscriptText.Text = "Last heard: " + message);
        _voice.CommandCompleted += _ => Dispatcher.Invoke(UpdateCommandContext);

        HistoryList.ItemsSource = _history.Items;

        _tray = new System.Windows.Forms.NotifyIcon
        {
            Text = "Nova Agent",
            Icon = SystemIcons.Application,
            Visible = true
        };

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Open Nova", null, (_, _) => Dispatcher.Invoke(ShowFromTray));
        menu.Items.Add("Start Listening", null, (_, _) => Dispatcher.Invoke(StartVoice));
        menu.Items.Add("Stop Listening", null, (_, _) => Dispatcher.Invoke(StopVoice));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(ExitApp));
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);

        LoadSettings();
        UpdateCommandContext();

        Loaded += async (_, _) =>
        {
            await RunDiagnosticsAsync();
            if (_safeMode)
            {
                StatusText.Text = "Safe mode — automatic listening is paused";
            }
            else if (_settingsService.Load().AlwaysListening)
                await _voice.StartAsync();
        };
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Load();

        AlwaysListeningCheck.IsChecked = settings.AlwaysListening;
        RequireWakeCheck.IsChecked = settings.RequireWakeWord;
        SpeakCheck.IsChecked = settings.SpeakResponses;
        AutoStartCheck.IsChecked = settings.AutoStart;
        CloseTrayCheck.IsChecked = settings.CloseToTray;
        WakeWordsBox.Text = string.Join(", ", settings.WakeWords);
        ConversationBox.Text = settings.ConversationSeconds.ToString();
        ChunkBox.Text = settings.ChunkMilliseconds.ToString();
        WhisperServerBox.Text = settings.WhisperServerPath;
        WhisperModelBox.Text = settings.WhisperModelPath;
        SpeechRateBox.Text = settings.SpeechRate.ToString();
        SpeechVolumeBox.Text = settings.SpeechVolume.ToString();
        FileSearchLimitBox.Text = settings.FileSearchLimit.ToString();
        CustomAppsBox.Text = string.Join(Environment.NewLine,
            settings.CustomApps.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"));

        try
        {
            var microphones = AudioCaptureService.GetInputDevices()
                .Select(device => new MicrophoneOption(device.Number, device.Name))
                .ToList();
            MicrophoneCombo.ItemsSource = microphones;
            MicrophoneCombo.SelectedValue = settings.MicrophoneDeviceNumber;
            if (MicrophoneCombo.SelectedIndex < 0 && microphones.Count > 0)
                MicrophoneCombo.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            AppLog.Error("Microphone list could not be loaded.", ex);
        }
    }

    private AppSettings ReadSettingsFromUi()
    {
        var current = _settingsService.Load();
        current.AlwaysListening = AlwaysListeningCheck.IsChecked == true;
        current.RequireWakeWord = RequireWakeCheck.IsChecked == true;
        current.SpeakResponses = SpeakCheck.IsChecked == true;
        current.AutoStart = AutoStartCheck.IsChecked == true;
        current.CloseToTray = CloseTrayCheck.IsChecked == true;
        current.WakeWords = WakeWordsBox.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (int.TryParse(ConversationBox.Text, out var conversation))
            current.ConversationSeconds = conversation;
        if (int.TryParse(ChunkBox.Text, out var chunk))
            current.ChunkMilliseconds = chunk;
        if (int.TryParse(SpeechRateBox.Text, out var rate))
            current.SpeechRate = rate;
        if (int.TryParse(SpeechVolumeBox.Text, out var volume))
            current.SpeechVolume = volume;
        if (int.TryParse(FileSearchLimitBox.Text, out var searchLimit))
            current.FileSearchLimit = searchLimit;
        if (MicrophoneCombo.SelectedValue is int microphone)
            current.MicrophoneDeviceNumber = microphone;

        current.WhisperServerPath = WhisperServerBox.Text.Trim();
        current.WhisperModelPath = WhisperModelBox.Text.Trim();
        current.CustomApps = ParseCustomApps(CustomAppsBox.Text);
        return current;
    }

    private static Dictionary<string, string> ParseCustomApps(string input)
    {
        var apps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in input.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (rawLine.StartsWith('#')) continue;
            var separator = rawLine.IndexOf('=');
            if (separator < 1 || separator == rawLine.Length - 1)
                throw new FormatException($"Invalid custom app entry: {rawLine}");

            var alias = rawLine[..separator].Trim().ToLowerInvariant();
            var path = rawLine[(separator + 1)..].Trim();
            if (alias.Any(character => !char.IsLetterOrDigit(character) && character is not ' ' and not '-' and not '_'))
                throw new FormatException($"Invalid app alias: {alias}");
            apps[alias] = path;
        }

        return apps;
    }

    private async void StartVoice(object? sender = null, RoutedEventArgs? e = null)
    {
        try { await _voice.StartAsync(); }
        catch (Exception ex) { ShowError("Voice listening could not start.", ex); }
    }

    private async void StopVoice(object? sender = null, RoutedEventArgs? e = null)
    {
        try { await _voice.StopAsync(); }
        catch (Exception ex) { ShowError("Voice listening could not stop cleanly.", ex); }
    }

    private void Start_Click(object sender, RoutedEventArgs e) => StartVoice(sender, e);
    private void Stop_Click(object sender, RoutedEventArgs e) => StopVoice(sender, e);

    private async void ListenOnce_Click(object sender, RoutedEventArgs e)
    {
        ListenOnceButton.IsEnabled = false;
        try { await _voice.ListenOnceAsync(); }
        catch (Exception ex) { ShowError("Listen once failed.", ex); }
        finally { ListenOnceButton.IsEnabled = true; }
    }

    private async Task RunCommandAsync(string command)
    {
        command = command.Trim();
        if (string.IsNullOrWhiteSpace(command)) return;

        StatusText.Text = "Running command...";
        try
        {
            var result = await _commands.ProcessAsync(command);
            _history.Add(command, result);
            _speech.Configure(_settingsService.Load());
            _speech.Speak(result.SpokenResponse);
            StatusText.Text = result.SpokenResponse;
            UpdateCommandContext();
        }
        catch (Exception ex)
        {
            ShowError("The command could not be completed.", ex);
        }
    }

    private async void RunTextCommand_Click(object sender, RoutedEventArgs e) =>
        await RunCommandAsync(CommandBox.Text);

    private async void CommandBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        await RunCommandAsync(CommandBox.Text);
    }

    private async void QuickCommand_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string command })
            await RunCommandAsync(command);
    }

    private void UpdateCommandContext() =>
        CurrentContextText.Text = "Current folder: " + _commands.CurrentDirectory;

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var settings = ReadSettingsFromUi();
            _settingsService.Save(settings);
            _speech.Configure(settings);
            _autoStart.Apply(settings.AutoStart);
            StatusText.Text = "Settings saved";
        }
        catch (Exception ex)
        {
            ShowError("Settings could not be saved. Check the highlighted values and custom app format.", ex);
        }
    }

    private void BrowseWhisperServer_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select whisper-server.exe",
            Filter = "Whisper server (whisper-server.exe)|whisper-server.exe|Executable files (*.exe)|*.exe"
        };
        if (dialog.ShowDialog(this) == true) WhisperServerBox.Text = dialog.FileName;
    }

    private void BrowseWhisperModel_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select a Whisper model",
            Filter = "Whisper model (*.bin)|*.bin|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) == true) WhisperModelBox.Text = dialog.FileName;
    }

    private void ExportSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export Nova Agent settings",
            FileName = "nova-settings.json",
            Filter = "JSON settings (*.json)|*.json"
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            _settingsService.Export(dialog.FileName, ReadSettingsFromUi());
            StatusText.Text = "Settings exported";
        }
        catch (Exception ex) { ShowError("Settings could not be exported.", ex); }
    }

    private void ImportSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import Nova Agent settings",
            Filter = "JSON settings (*.json)|*.json"
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            _settingsService.Import(dialog.FileName);
            LoadSettings();
            StatusText.Text = "Settings imported — review and save";
        }
        catch (Exception ex) { ShowError("The selected settings file is invalid.", ex); }
    }

    private void ExportHistory_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export command history",
            FileName = $"nova-history-{DateTime.Now:yyyyMMdd}.csv",
            Filter = "CSV file (*.csv)|*.csv"
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            _history.ExportCsv(dialog.FileName);
            StatusText.Text = "History exported";
        }
        catch (Exception ex) { ShowError("History could not be exported.", ex); }
    }

    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Permanently clear local command history?", "Nova Agent",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            _history.Clear();
            StatusText.Text = "History cleared";
        }
        catch (Exception ex) { ShowError("History could not be cleared.", ex); }
    }

    private async Task RunDiagnosticsAsync()
    {
        DiagnosticsSummaryText.Text = "Checking this PC...";
        try
        {
            var checks = await _diagnostics.RunAsync(_settingsService.Load());
            DiagnosticsList.ItemsSource = checks;
            var passed = checks.Count(check => check.Passed);
            DiagnosticsSummaryText.Text = $"{passed} of {checks.Count} checks passed";
        }
        catch (Exception ex)
        {
            DiagnosticsSummaryText.Text = "Diagnostics failed — see log";
            AppLog.Error("Diagnostics failed.", ex);
        }
    }

    private async void RunDiagnostics_Click(object sender, RoutedEventArgs e) => await RunDiagnosticsAsync();

    private void OpenAppData_Click(object sender, RoutedEventArgs e) =>
        _windows.OpenFolder(_settingsService.DataDirectory);

    private void OpenLog_Click(object sender, RoutedEventArgs e)
    {
        AppLog.Info("Log opened from Diagnostics.");
        _windows.OpenPath(AppLog.CurrentLogPath);
    }

    private void ShowError(string message, Exception exception)
    {
        AppLog.Error(message, exception);
        StatusText.Text = message;
        MessageBox.Show($"{message}\n\n{exception.Message}", "Nova Agent",
            MessageBoxButton.OK, MessageBoxImage.Error);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_exitRequested)
        {
            base.OnClosing(e);
            return;
        }

        if (_settingsService.Load().CloseToTray)
        {
            e.Cancel = true;
            Hide();
            _tray.ShowBalloonTip(1200, "Nova Agent",
                "Nova is still running in the system tray.",
                System.Windows.Forms.ToolTipIcon.Info);
            return;
        }

        e.Cancel = true;
        ExitApp();
    }

    private void ShowFromTray()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    public void RestoreFromExternalActivation() => ShowFromTray();

    public void HideForStartup()
    {
        ShowInTaskbar = false;
        Hide();
    }

    private async void ExitApp()
    {
        if (_exitRequested) return;
        _exitRequested = true;

        try { await _voice.StopAsync(); }
        catch (Exception ex) { AppLog.Error("Voice loop did not stop cleanly during exit.", ex); }

        _tray.Visible = false;
        _tray.Dispose();
        _speech.Dispose();
        _whisper.Dispose();
        System.Windows.Application.Current.Shutdown();
    }
}
