using System.Text;

namespace NovaAgent.Services;

public static class AppLog
{
    private const long MaximumLogBytes = 5L * 1024 * 1024;
    private static readonly object Sync = new();
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NovaAgent", "Logs");

    public static string CurrentLogPath => Path.Combine(DirectoryPath, $"nova-{DateTime.Now:yyyyMMdd}.log");

    public static void Info(string message) => Write("INFO", message, null);
    public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var line = new StringBuilder()
                .Append(DateTimeOffset.Now.ToString("O"))
                .Append(" [").Append(level).Append("] ")
                .Append(message);
            if (exception is not null)
                line.AppendLine().Append(exception);

            lock (Sync)
            {
                RotateIfNeeded();
                File.AppendAllText(CurrentLogPath, line.AppendLine().ToString());
            }
        }
        catch
        {
            // Logging must never crash the assistant.
        }
    }

    private static void RotateIfNeeded()
    {
        var path = CurrentLogPath;
        if (!File.Exists(path) || new FileInfo(path).Length < MaximumLogBytes) return;

        var archive = path + ".1";
        File.Move(path, archive, overwrite: true);
    }
}
