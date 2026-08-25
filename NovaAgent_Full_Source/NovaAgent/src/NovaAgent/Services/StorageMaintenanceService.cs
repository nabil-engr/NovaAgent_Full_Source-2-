namespace NovaAgent.Services;

public static class StorageMaintenanceService
{
    private static readonly TimeSpan TempRetention = TimeSpan.FromDays(1);
    private static readonly TimeSpan LogRetention = TimeSpan.FromDays(14);

    public static void Run()
    {
        try
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NovaAgent");

            DeleteOldFiles(Path.Combine(root, "Temp"), "voice-*.wav", TempRetention);
            DeleteOldFiles(Path.Combine(root, "Logs"), "nova-*.log*", LogRetention);
        }
        catch (Exception ex)
        {
            AppLog.Error("Local storage maintenance failed.", ex);
        }
    }

    private static void DeleteOldFiles(string directory, string pattern, TimeSpan retention)
    {
        if (!Directory.Exists(directory)) return;

        var cutoff = DateTime.UtcNow - retention;
        foreach (var file in Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                    File.Delete(file);
            }
            catch (Exception ex)
            {
                AppLog.Error($"Could not remove stale local file: {Path.GetFileName(file)}", ex);
            }
        }
    }
}
