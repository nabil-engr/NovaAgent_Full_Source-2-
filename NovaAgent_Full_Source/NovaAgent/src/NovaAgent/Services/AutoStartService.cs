using Microsoft.Win32;

namespace NovaAgent.Services;

public sealed class AutoStartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "NovaAgent";

    public void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKey);

        if (enabled)
        {
            var exe = Environment.ProcessPath ?? "";
            key.SetValue(ValueName, $"\"{exe}\" --minimized");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
