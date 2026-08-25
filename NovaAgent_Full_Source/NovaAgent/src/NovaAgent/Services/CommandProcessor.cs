using System.Globalization;
using System.Text.RegularExpressions;
using NovaAgent.Models;

namespace NovaAgent.Services;

public sealed class CommandProcessor
{
    private readonly WindowsControlService _windows;
    private readonly FileSearchService _files;
    private readonly SettingsService _settings;

    private PendingConfirmation? _pending;

    public string CurrentDirectory { get; private set; }

    public CommandProcessor(
        WindowsControlService windows,
        FileSearchService files,
        SettingsService settings)
    {
        _windows = windows;
        _files = files;
        _settings = settings;
        CurrentDirectory = windows.Downloads;
    }

    public async Task<CommandResult> ProcessAsync(string raw)
    {
        var command = Normalize(raw);

        if (string.IsNullOrWhiteSpace(command))
            return Fail("I didn't catch that.");

        if (_pending is not null)
        {
            if (_pending.ExpiresUtc < DateTime.UtcNow)
                _pending = null;
            else if (ContainsAny(command, "confirm", "yes", "do it", "করো", "হ্যাঁ", "হ্যা", "কনফার্ম"))
            {
                var action = _pending;
                _pending = null;
                return await action.ExecuteAsync();
            }
            else if (ContainsAny(command, "cancel", "no", "না", "বাদ দাও", "ক্যানসেল"))
            {
                _pending = null;
                return Ok("Cancelled.");
            }
        }

        var settings = _settings.Load();

        // Help
        if (ContainsAny(command, "what can you do", "help", "কি করতে পারো", "হেল্প"))
            return Ok("I can open and find files, launch built-in or custom apps, control media, volume, browser tabs and keyboard shortcuts, search the web, manage windows, and safely handle protected system actions.");

        // Volume
        if (ContainsAny(command, "volume", "ভলিউম", "sound", "শব্দ"))
        {
            var number = Regex.Match(command, @"\b(100|\d{1,2})\b");
            if (number.Success && int.TryParse(number.Value, out var volume))
            {
                var applied = _windows.SetVolume(volume);
                return Ok($"Volume set to {applied} percent.");
            }

            if (ContainsAny(command, "up", "increase", "বাড়াও", "বাড়াও", "barau", "raise"))
            {
                var now = _windows.ChangeVolume(10);
                return Ok($"Volume {now} percent.");
            }

            if (ContainsAny(command, "down", "decrease", "কমাও", "komao", "lower"))
            {
                var now = _windows.ChangeVolume(-10);
                return Ok($"Volume {now} percent.");
            }
        }

        if (ContainsAny(command, "mute", "মিউট"))
        {
            _windows.ToggleMute();
            return Ok("Mute toggled.");
        }

        // Media
        if (ContainsAny(command, "pause", "পজ", "থামাও", "play", "resume", "চালাও") &&
            !LooksLikeFileCommand(command))
        {
            _windows.MediaPlayPause();
            return Ok(ContainsAny(command, "pause", "পজ", "থামাও") ? "Paused." : "Play or pause toggled.");
        }

        if (ContainsAny(command, "next song", "next track", "পরের গান", "নেক্সট"))
        {
            _windows.MediaNext();
            return Ok("Next track.");
        }

        if (ContainsAny(command, "previous song", "previous track", "আগের গান", "প্রিভিয়াস"))
        {
            _windows.MediaPrevious();
            return Ok("Previous track.");
        }

        // Window controls
        if (ContainsAny(command, "switch window", "alt tab", "উইন্ডো বদলাও"))
        {
            _windows.AltTab();
            return Ok("Switched window.");
        }

        if (ContainsAny(command, "minimize", "মিনিমাইজ"))
        {
            _windows.MinimizeForeground();
            return Ok("Minimized.");
        }

        if (ContainsAny(command, "maximize", "ম্যাক্সিমাইজ"))
        {
            _windows.MaximizeForeground();
            return Ok("Maximized.");
        }

        if (ContainsAny(command, "restore window", "রিস্টোর"))
        {
            _windows.RestoreForeground();
            return Ok("Restored.");
        }

        // Common keyboard and browser shortcuts. Keep these explicit and allow-listed.
        if (MatchesAny(command, "copy", "copy selection", "কপি করো")) { _windows.Copy(); return Ok("Copied."); }
        if (MatchesAny(command, "cut", "cut selection", "কাট করো")) { _windows.Cut(); return Ok("Cut."); }
        if (MatchesAny(command, "paste", "পেস্ট করো")) { _windows.Paste(); return Ok("Pasted."); }
        if (MatchesAny(command, "save", "save file", "সেভ করো")) { _windows.Save(); return Ok("Saved."); }
        if (MatchesAny(command, "undo", "আনডু")) { _windows.Undo(); return Ok("Undone."); }
        if (MatchesAny(command, "redo", "রিডু")) { _windows.Redo(); return Ok("Redone."); }
        if (MatchesAny(command, "select all", "সব সিলেক্ট")) { _windows.SelectAll(); return Ok("Selected all."); }
        if (MatchesAny(command, "refresh", "refresh page", "রিফ্রেশ")) { _windows.BrowserRefresh(); return Ok("Refreshed."); }
        if (MatchesAny(command, "browser back", "go back", "আগের পেজ")) { _windows.BrowserBack(); return Ok("Going back."); }
        if (MatchesAny(command, "browser forward", "go forward", "পরের পেজ")) { _windows.BrowserForward(); return Ok("Going forward."); }
        if (MatchesAny(command, "new tab", "নতুন ট্যাব")) { _windows.NewTab(); return Ok("New tab."); }
        if (MatchesAny(command, "close tab", "ট্যাব বন্ধ")) { _windows.CloseTab(); return Ok("Tab closed."); }

        if (ContainsAny(command, "current folder", "working folder", "এখনকার ফোল্ডার") &&
            ContainsAny(command, "open", "খুলো", "show"))
        {
            _windows.OpenFolder(CurrentDirectory);
            return Ok("Opening the current folder.", CurrentDirectory);
        }

        if (ContainsAny(command, "recycle bin", "রিসাইকেল বিন") &&
            ContainsAny(command, "open", "খুলো"))
        {
            _windows.OpenRecycleBin();
            return Ok("Opening Recycle Bin.");
        }

        // Special folders
        var folder = ResolveNamedFolder(command);
        if (folder is not null &&
            ContainsAny(command, "open", "go", "folder", "খুলো", "যাও", "ফোল্ডার", "jao", "koro"))
        {
            CurrentDirectory = folder.Value.Path;
            _windows.OpenFolder(CurrentDirectory);
            return Ok($"Opened {folder.Value.Name}.", CurrentDirectory);
        }

        // Latest file
        var latestMatch = Regex.Match(command,
            @"(?:latest|newest|সর্বশেষ|নতুন)\s+(?<ext>pdf|mp4|mp3|docx|xlsx|jpg|jpeg|png|zip)\b",
            RegexOptions.IgnoreCase);

        if (latestMatch.Success)
        {
            var path = await _files.FindLatestAsync(
                CurrentDirectory, latestMatch.Groups["ext"].Value, settings.FileSearchLimit);
            if (path is null)
                return Fail($"I couldn't find a recent {latestMatch.Groups["ext"].Value} file.");

            _windows.OpenPath(path);
            return Ok($"Opening {Path.GetFileName(path)}.", path);
        }

        // File open - explicit extension.
        var explicitFile = ExtractFileName(command);
        if (explicitFile is not null && ContainsAny(command, "open", "খুলো", "চালাও", "play", "open koro"))
        {
            var path = await _files.FindFileAsync(CurrentDirectory, explicitFile, settings.FileSearchLimit);
            if (path is null && CurrentDirectory != _windows.Downloads)
                path = await _files.FindFileAsync(_windows.Downloads, explicitFile, settings.FileSearchLimit);

            if (path is null)
                return Fail($"I couldn't find {explicitFile}.");

            _windows.OpenPath(path);
            CurrentDirectory = Path.GetDirectoryName(path) ?? CurrentDirectory;
            return Ok($"Opening {Path.GetFileName(path)}.", path);
        }

        // Apps
        foreach (var app in new[]
                 {
                     "google chrome", "chrome", "microsoft edge", "edge", "visual studio code",
                     "vs code", "vscode", "notepad", "calculator", "calc", "file explorer",
                     "explorer", "task manager", "settings", "paint", "terminal"
                 })
        {
            if (command.Contains(app, StringComparison.OrdinalIgnoreCase) &&
                ContainsAny(command, "open", "launch", "start", "খুলো", "চালু", "চালাও"))
            {
                var mapped = app switch
                {
                    "google chrome" => "chrome",
                    "microsoft edge" => "edge",
                    _ => app
                };
                return _windows.LaunchApp(mapped)
                    ? Ok($"Opening {app}.")
                    : Fail($"I couldn't launch {app}.");
            }
        }

        // User-defined aliases are configured as one "alias=path" pair per line.
        foreach (var app in settings.CustomApps.OrderByDescending(pair => pair.Key.Length))
        {
            if (ContainsPhrase(command, app.Key) &&
                ContainsAny(command, "open", "launch", "start", "খুলো", "চালু", "চালাও"))
            {
                return _windows.LaunchCustomApp(app.Value)
                    ? Ok($"Opening {app.Key}.")
                    : Fail($"I couldn't launch {app.Key}. Check its path in Settings.");
            }
        }

        // Search
        var youtube = Regex.Match(command,
            @"(?:youtube|ইউটিউব).*(?:search|খুঁজ|খোজ|search for)\s+(?<q>.+)",
            RegexOptions.IgnoreCase);
        if (youtube.Success)
        {
            var q = CleanTail(youtube.Groups["q"].Value);
            _windows.YouTubeSearch(q);
            return Ok($"Searching YouTube for {q}.");
        }

        var google = Regex.Match(command,
            @"(?:google|গুগল).*(?:search|খুঁজ|খোজ|search for)\s+(?<q>.+)",
            RegexOptions.IgnoreCase);
        if (google.Success)
        {
            var q = CleanTail(google.Groups["q"].Value);
            _windows.GoogleSearch(q);
            return Ok($"Searching Google for {q}.");
        }

        if (command.StartsWith("search ", StringComparison.OrdinalIgnoreCase))
        {
            var q = command[7..].Trim();
            _windows.GoogleSearch(q);
            return Ok($"Searching for {q}.");
        }

        // URLs
        var url = Regex.Match(command, @"\b(?:https?://)?(?:www\.)?[a-z0-9-]+\.(?:com|org|net|io|ai|dev|app|co)(?:/[^\s]*)?\b",
            RegexOptions.IgnoreCase);
        if (url.Success && ContainsAny(command, "open", "go", "খুলো", "যাও"))
        {
            _windows.OpenUrl(url.Value);
            return Ok($"Opening {url.Value}.");
        }

        // Type text
        var type = Regex.Match(raw.Trim(), @"^(?:type|write|লিখো|টাইপ করো)\s+(?<text>.+)$",
            RegexOptions.IgnoreCase);
        if (type.Success)
        {
            var text = type.Groups["text"].Value.Trim();
            _windows.TypeText(text);
            return Ok("Typed it.");
        }

        // Create folder
        var createFolder = Regex.Match(raw.Trim(),
            @"(?:create|make|তৈরি করো|বানাও)\s+(?:a\s+)?(?:new\s+)?(?:folder|ফোল্ডার)\s+(?:named\s+)?(?<name>.+)$",
            RegexOptions.IgnoreCase);
        if (createFolder.Success)
        {
            var name = SanitizeFileName(createFolder.Groups["name"].Value);
            if (string.IsNullOrWhiteSpace(name))
                return Fail("Please say a folder name.");

            var path = Path.Combine(CurrentDirectory, name);
            Directory.CreateDirectory(path);
            return Ok($"Created folder {name}.", path);
        }

        // Screen snip
        if (ContainsAny(command, "screenshot", "screen shot", "স্ক্রিনশট"))
        {
            _windows.OpenScreenSnip();
            return Ok("Opening screen capture.");
        }

        // Time/date
        if (ContainsAny(command, "what time", "time now", "কয়টা বাজে", "কয়টা বাজে"))
            return Ok($"It is {DateTime.Now:h:mm tt}.");

        if (ContainsAny(command, "what date", "today date", "আজ কত তারিখ"))
            return Ok($"Today is {DateTime.Now:dddd, d MMMM yyyy}.");

        // Protected system actions.
        if (ContainsAny(command, "shutdown", "shut down", "কম্পিউটার বন্ধ", "পিসি বন্ধ"))
            return RequireConfirmation("shutdown the PC", () =>
            {
                _windows.Shutdown();
                return Task.FromResult(Ok("Shutting down."));
            });

        if (ContainsAny(command, "restart", "রিস্টার্ট"))
            return RequireConfirmation("restart the PC", () =>
            {
                _windows.Restart();
                return Task.FromResult(Ok("Restarting."));
            });

        if (ContainsAny(command, "sleep pc", "sleep computer", "পিসি স্লিপ"))
            return RequireConfirmation("put the PC to sleep", () =>
            {
                _windows.Sleep();
                return Task.FromResult(Ok("Putting the PC to sleep."));
            });

        if (ContainsAny(command, "lock pc", "lock computer", "পিসি লক"))
        {
            _windows.LockPc();
            return Ok("PC locked.");
        }

        return Fail("I understood the speech, but I don't have a safe command for that yet.",
            $"Unmatched command: {raw}");
    }

    private CommandResult RequireConfirmation(string description, Func<Task<CommandResult>> action)
    {
        _pending = new PendingConfirmation
        {
            Description = description,
            ExecuteAsync = action
        };

        return new CommandResult(
            true,
            $"Please say confirm if you want me to {description}.",
            description,
            true);
    }

    private (string Name, string Path)? ResolveNamedFolder(string command)
    {
        if (ContainsAny(command, "downloads", "download folder", "ডাউনলোড", "ডাউনলোডস"))
            return ("Downloads", _windows.Downloads);
        if (ContainsAny(command, "desktop", "ডেস্কটপ"))
            return ("Desktop", _windows.Desktop);
        if (ContainsAny(command, "documents", "document folder", "ডকুমেন্ট"))
            return ("Documents", _windows.Documents);
        if (ContainsAny(command, "pictures", "picture folder", "ছবি ফোল্ডার"))
            return ("Pictures", _windows.Pictures);
        if (ContainsAny(command, "music folder", "মিউজিক ফোল্ডার"))
            return ("Music", _windows.Music);
        if (ContainsAny(command, "videos folder", "video folder", "ভিডিও ফোল্ডার"))
            return ("Videos", _windows.Videos);

        return null;
    }

    private static bool LooksLikeFileCommand(string command) =>
        Regex.IsMatch(command, @"\.(mp4|mp3|wav|mkv|pdf|docx|xlsx|pptx|txt)\b",
            RegexOptions.IgnoreCase);

    private static string? ExtractFileName(string command)
    {
        var normalized = command
            .Replace(" dot mp4", ".mp4", StringComparison.OrdinalIgnoreCase)
            .Replace(" dot mp3", ".mp3", StringComparison.OrdinalIgnoreCase)
            .Replace(" dot pdf", ".pdf", StringComparison.OrdinalIgnoreCase)
            .Replace(" dot mkv", ".mkv", StringComparison.OrdinalIgnoreCase)
            .Replace(" dot docx", ".docx", StringComparison.OrdinalIgnoreCase)
            .Replace(" dot xlsx", ".xlsx", StringComparison.OrdinalIgnoreCase)
            .Replace(" dot pptx", ".pptx", StringComparison.OrdinalIgnoreCase);

        var match = Regex.Match(normalized,
            @"(?<file>[\p{L}\p{N}_\-\s\(\)\[\]]+\.(?:mp4|mp3|wav|mkv|avi|pdf|docx|xlsx|pptx|txt|jpg|jpeg|png|zip))",
            RegexOptions.IgnoreCase);

        if (!match.Success) return null;

        var value = match.Groups["file"].Value.Trim();
        value = Regex.Replace(value,
            @"^(?:open|play|খুলো|চালাও|please|প্লিজ)\s+",
            "",
            RegexOptions.IgnoreCase);

        return value.Trim();
    }

    private static string SanitizeFileName(string value)
    {
        value = value.Trim().Trim('.', '।');
        foreach (var c in Path.GetInvalidFileNameChars())
            value = value.Replace(c.ToString(), "");
        return value;
    }

    private static string Normalize(string input)
    {
        var v = input.Trim().ToLowerInvariant()
            .Replace("।", " ")
            .Replace("?", " ")
            .Replace("!", " ")
            .Replace(",", " ");
        return Regex.Replace(v, @"\s+", " ").Trim();
    }

    private static string CleanTail(string value) =>
        value.Trim().Trim('.', ',', '?', '!', '।');

    private static bool ContainsAny(string input, params string[] needles) =>
        needles.Any(n => input.Contains(n, StringComparison.OrdinalIgnoreCase));

    private static bool MatchesAny(string input, params string[] phrases) =>
        phrases.Any(phrase => string.Equals(input, phrase, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsPhrase(string input, string phrase) =>
        Regex.IsMatch(input, $@"(?:^|\s){Regex.Escape(phrase)}(?:$|\s)", RegexOptions.IgnoreCase);

    private static CommandResult Ok(string response, string detail = "") =>
        new(true, response, detail);

    private static CommandResult Fail(string response, string detail = "") =>
        new(false, response, detail);
}
