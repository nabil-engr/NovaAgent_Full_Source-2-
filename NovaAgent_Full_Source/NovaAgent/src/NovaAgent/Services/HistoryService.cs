using System.Collections.ObjectModel;
using System.Text.Json;
using NovaAgent.Models;

namespace NovaAgent.Services;

public sealed class HistoryService
{
    private const int MaximumDiskEntries = 2000;
    private const long CompactThresholdBytes = 2L * 1024 * 1024;
    private readonly object _fileSync = new();
    private readonly string _file;
    public string HistoryPath => _file;
    public ObservableCollection<HistoryEntry> Items { get; } = new();

    public HistoryService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NovaAgent");
        Directory.CreateDirectory(dir);
        _file = Path.Combine(dir, "history.jsonl");
        LoadRecent();
    }

    public void Add(string command, CommandResult result)
    {
        var entry = new HistoryEntry
        {
            Time = DateTime.Now,
            Command = command,
            Result = string.IsNullOrWhiteSpace(result.Detail) ? result.SpokenResponse : result.Detail,
            Success = result.Success
        };

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            Items.Insert(0, entry);
            while (Items.Count > 100)
                Items.RemoveAt(Items.Count - 1);
        });

        try
        {
            lock (_fileSync)
            {
                File.AppendAllText(_file, JsonSerializer.Serialize(entry) + Environment.NewLine);
                if (new FileInfo(_file).Length >= CompactThresholdBytes)
                    CompactHistory();
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Command history could not be written.", ex);
        }
    }

    public void Clear()
    {
        Items.Clear();
        try
        {
            lock (_fileSync)
            {
                if (File.Exists(_file))
                    File.Delete(_file);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Command history could not be cleared.", ex);
            throw;
        }
    }

    public void ExportCsv(string destination)
    {
        static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

        var rows = new List<string> { "Time,Success,Command,Result" };
        rows.AddRange(Items.Select(item => string.Join(",",
            Csv(item.Time.ToString("O")),
            item.Success ? "true" : "false",
            Csv(item.Command),
            Csv(item.Result))));
        File.WriteAllLines(destination, rows);
    }

    private void LoadRecent()
    {
        if (!File.Exists(_file)) return;

        try
        {
            var recent = new Queue<string>(100);
            foreach (var line in File.ReadLines(_file))
            {
                if (recent.Count == 100) recent.Dequeue();
                recent.Enqueue(line);
            }

            foreach (var line in recent.Reverse())
            {
                var item = JsonSerializer.Deserialize<HistoryEntry>(line);
                if (item is not null) Items.Add(item);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Command history could not be loaded.", ex);
        }
    }

    private void CompactHistory()
    {
        var recent = new Queue<string>(MaximumDiskEntries);
        foreach (var line in File.ReadLines(_file))
        {
            if (recent.Count == MaximumDiskEntries) recent.Dequeue();
            recent.Enqueue(line);
        }

        var temporary = _file + ".tmp";
        File.WriteAllLines(temporary, recent);
        File.Move(temporary, _file, overwrite: true);
        AppLog.Info($"Command history compacted to {recent.Count} entries.");
    }
}
