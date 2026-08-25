namespace NovaAgent.Services;

public sealed class FileSearchService
{
    public Task<string?> FindFileAsync(
        string folder,
        string requested,
        int fileLimit,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => FindFile(folder, requested, fileLimit, cancellationToken), cancellationToken);

    public Task<string?> FindLatestAsync(
        string folder,
        string extension,
        int fileLimit,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => FindLatest(folder, extension, fileLimit, cancellationToken), cancellationToken);

    private static string? FindFile(
        string folder,
        string requested,
        int fileLimit,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(folder)) return null;

        requested = requested.Trim().Trim('"', '\'', '।', '.', ',');
        requested = requested.Replace(" dot ", ".", StringComparison.OrdinalIgnoreCase);

        var direct = Path.Combine(folder, requested);
        if (File.Exists(direct))
            return direct;

        string? partial = null;
        var requestedStem = Path.GetFileNameWithoutExtension(requested);
        foreach (var file in EnumerateFilesSafe(folder, fileLimit, cancellationToken))
        {
            if (string.Equals(Path.GetFileName(file), requested, StringComparison.OrdinalIgnoreCase))
                return file;

            if (partial is null && Path.GetFileNameWithoutExtension(file)
                    .Contains(requestedStem, StringComparison.OrdinalIgnoreCase))
                partial = file;
        }

        return partial;
    }

    private static string? FindLatest(
        string folder,
        string extension,
        int fileLimit,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(folder)) return null;

        var ext = extension.StartsWith('.') ? extension : "." + extension;
        string? latest = null;
        var latestTime = DateTime.MinValue;

        foreach (var file in EnumerateFilesSafe(folder, fileLimit, cancellationToken))
        {
            if (!string.Equals(Path.GetExtension(file), ext, StringComparison.OrdinalIgnoreCase))
                continue;

            DateTime writeTime;
            try { writeTime = File.GetLastWriteTimeUtc(file); }
            catch { continue; }

            if (writeTime <= latestTime) continue;
            latestTime = writeTime;
            latest = file;
        }

        return latest;
    }

    private static IEnumerable<string> EnumerateFilesSafe(
        string root,
        int fileLimit,
        CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        var visited = 0;

        while (pending.Count > 0 && visited < fileLimit)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();

            string[] files;
            try { files = Directory.GetFiles(directory); }
            catch { continue; }

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++visited > fileLimit) yield break;
                yield return file;
            }

            string[] children;
            try { children = Directory.GetDirectories(directory); }
            catch { continue; }

            foreach (var child in children)
                try
                {
                    if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0)
                        pending.Push(child);
                }
                catch { }
        }
    }
}
