using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using NAudio.CoreAudioApi;

namespace NovaAgent.Services;

public sealed class WindowsControlService
{
    private const byte VK_MEDIA_NEXT_TRACK = 0xB0;
    private const byte VK_MEDIA_PREV_TRACK = 0xB1;
    private const byte VK_MEDIA_PLAY_PAUSE = 0xB3;
    private const byte VK_VOLUME_MUTE = 0xAD;
    private const byte VK_MENU = 0x12;
    private const byte VK_TAB = 0x09;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private const int SW_MINIMIZE = 6;
    private const int SW_MAXIMIZE = 3;
    private const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool LockWorkStation();

    public string Downloads =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    public string Desktop => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    public string Documents => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    public string Pictures => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    public string Music => Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
    public string Videos => Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

    public void OpenPath(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    public void OpenFolder(string path) => OpenPath(path);

    public bool LaunchApp(string name)
    {
        var candidates = name.ToLowerInvariant() switch
        {
            "chrome" => new[]
            {
                "chrome.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    @"Google\Chrome\Application\chrome.exe")
            },
            "edge" => ["msedge.exe"],
            "vscode" or "vs code" or "visual studio code" => new[]
            {
                "code.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    @"Programs\Microsoft VS Code\Code.exe")
            },
            "notepad" => ["notepad.exe"],
            "calculator" or "calc" => ["calc.exe"],
            "explorer" or "file explorer" => ["explorer.exe"],
            "task manager" => ["taskmgr.exe"],
            "settings" => ["ms-settings:"],
            "paint" => ["mspaint.exe"],
            "terminal" => ["wt.exe", "powershell.exe"],
            _ => [name]
        };

        foreach (var candidate in candidates)
        {
            try
            {
                if (candidate.Contains(Path.DirectorySeparatorChar) && !File.Exists(candidate))
                    continue;

                Process.Start(new ProcessStartInfo
                {
                    FileName = candidate,
                    UseShellExecute = true
                });
                return true;
            }
            catch { }
        }

        return false;
    }

    public bool LaunchCustomApp(string configuredPath)
    {
        var candidate = Environment.ExpandEnvironmentVariables(configuredPath.Trim().Trim('"'));
        if (string.IsNullOrWhiteSpace(candidate)) return false;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = candidate,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Error($"Custom app could not be launched: {candidate}", ex);
            return false;
        }
    }

    public void OpenUrl(string url)
    {
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url;

        OpenPath(url);
    }

    public void GoogleSearch(string query) =>
        OpenUrl("https://www.google.com/search?q=" + Uri.EscapeDataString(query));

    public void YouTubeSearch(string query) =>
        OpenUrl("https://www.youtube.com/results?search_query=" + Uri.EscapeDataString(query));

    public int GetVolume()
    {
        using var enumerator = new MMDeviceEnumerator();
        using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        return (int)Math.Round(device.AudioEndpointVolume.MasterVolumeLevelScalar * 100);
    }

    public int SetVolume(int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        using var enumerator = new MMDeviceEnumerator();
        using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        device.AudioEndpointVolume.MasterVolumeLevelScalar = percent / 100f;
        return percent;
    }

    public int ChangeVolume(int delta) => SetVolume(GetVolume() + delta);

    public void ToggleMute()
    {
        using var enumerator = new MMDeviceEnumerator();
        using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        device.AudioEndpointVolume.Mute = !device.AudioEndpointVolume.Mute;
    }

    public void MediaPlayPause() => PressKey(VK_MEDIA_PLAY_PAUSE);
    public void MediaNext() => PressKey(VK_MEDIA_NEXT_TRACK);
    public void MediaPrevious() => PressKey(VK_MEDIA_PREV_TRACK);
    public void MediaMuteKey() => PressKey(VK_VOLUME_MUTE);

    public void AltTab()
    {
        keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
        keybd_event(VK_TAB, 0, 0, UIntPtr.Zero);
        keybd_event(VK_TAB, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    public void MinimizeForeground()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd != IntPtr.Zero) ShowWindow(hwnd, SW_MINIMIZE);
    }

    public void MaximizeForeground()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd != IntPtr.Zero) ShowWindow(hwnd, SW_MAXIMIZE);
    }

    public void RestoreForeground()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd != IntPtr.Zero) ShowWindow(hwnd, SW_RESTORE);
    }

    public void TypeText(string text)
    {
        System.Windows.Forms.SendKeys.SendWait(EscapeSendKeys(text));
    }

    public void Copy() => SendShortcut("^c");
    public void Cut() => SendShortcut("^x");
    public void Paste() => SendShortcut("^v");
    public void Save() => SendShortcut("^s");
    public void Undo() => SendShortcut("^z");
    public void Redo() => SendShortcut("^y");
    public void SelectAll() => SendShortcut("^a");
    public void BrowserRefresh() => SendShortcut("{F5}");
    public void BrowserBack() => SendShortcut("%{LEFT}");
    public void BrowserForward() => SendShortcut("%{RIGHT}");
    public void NewTab() => SendShortcut("^t");
    public void CloseTab() => SendShortcut("^w");

    public void LockPc() => LockWorkStation();

    public void Shutdown() =>
        Process.Start(new ProcessStartInfo("shutdown.exe", "/s /t 0")
        { UseShellExecute = false, CreateNoWindow = true });

    public void Restart() =>
        Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 0")
        { UseShellExecute = false, CreateNoWindow = true });

    public void Sleep() =>
        Process.Start(new ProcessStartInfo("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0")
        { UseShellExecute = false, CreateNoWindow = true });

    public void OpenScreenSnip() =>
        Process.Start(new ProcessStartInfo { FileName = "ms-screenclip:", UseShellExecute = true });

    public void OpenRecycleBin() =>
        Process.Start(new ProcessStartInfo { FileName = "shell:RecycleBinFolder", UseShellExecute = true });

    private static void PressKey(byte key)
    {
        keybd_event(key, 0, 0, UIntPtr.Zero);
        keybd_event(key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    private static void SendShortcut(string keys)
    {
        try { System.Windows.Forms.SendKeys.SendWait(keys); }
        catch (Exception ex) { AppLog.Error($"Keyboard shortcut failed: {keys}", ex); }
    }

    private static string EscapeSendKeys(string value)
    {
        var sb = new StringBuilder();
        foreach (var c in value)
        {
            if ("+^%~()[]{}".Contains(c))
                sb.Append('{').Append(c).Append('}');
            else
                sb.Append(c);
        }
        return sb.ToString();
    }
}
