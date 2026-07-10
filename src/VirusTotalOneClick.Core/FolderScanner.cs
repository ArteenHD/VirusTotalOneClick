namespace VirusTotalOneClick.Core;

public static class FolderScanner
{
    public static FolderScanResult Scan(
        string rootPath,
        FolderScanOptions options,
        CancellationToken cancellationToken = default)
        => Scan(
            rootPath,
            options,
            Directory.EnumerateFiles,
            Directory.EnumerateDirectories,
            File.GetAttributes,
            cancellationToken);

    internal static FolderScanResult Scan(
        string rootPath,
        FolderScanOptions options,
        Func<string, IEnumerable<string>> enumerateFiles,
        Func<string, IEnumerable<string>> enumerateDirectories,
        Func<string, FileAttributes> getAttributes,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (options.MaxFiles <= 0)
        {
            return new FolderScanResult([], Truncated: true, []);
        }

        var files = new List<string>();
        var errors = new List<string>();
        var pending = new Queue<string>();
        pending.Enqueue(rootPath);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Dequeue();

            if (IsReparsePoint(directory, getAttributes, errors))
            {
                continue;
            }

            if (AddFiles(directory, options.MaxFiles, enumerateFiles, files, errors, cancellationToken))
            {
                return new FolderScanResult(files, Truncated: true, errors);
            }

            foreach (var child in EnumerateSafely(directory, enumerateDirectories, errors))
            {
                pending.Enqueue(child);
            }
        }

        return new FolderScanResult(files, Truncated: false, errors);
    }

    private static bool AddFiles(
        string directory,
        int maxFiles,
        Func<string, IEnumerable<string>> enumerateFiles,
        List<string> files,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        if (files.Count >= maxFiles)
        {
            return true;
        }

        try
        {
            foreach (var file in enumerateFiles(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();

                files.Add(file);

                if (files.Count >= maxFiles)
                {
                    return true;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            errors.Add($"{directory}: {ex.Message}");
        }

        return false;
    }

    private static List<string> EnumerateSafely(
        string directory,
        Func<string, IEnumerable<string>> enumerate,
        List<string> errors)
    {
        var paths = new List<string>();

        try
        {
            paths.AddRange(enumerate(directory));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            errors.Add($"{directory}: {ex.Message}");
        }

        return paths;
    }

    private static bool IsReparsePoint(
        string directory,
        Func<string, FileAttributes> getAttributes,
        List<string> errors)
    {
        try
        {
            return getAttributes(directory).HasFlag(FileAttributes.ReparsePoint);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            errors.Add($"{directory}: {ex.Message}");
            return true;
        }
    }
}
