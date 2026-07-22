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
        var isRoot = true;

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Dequeue();
            var isThisDirectoryTheRoot = isRoot;
            isRoot = false;

            if (IsReparsePoint(directory, isThisDirectoryTheRoot, getAttributes, errors))
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
        try
        {
            // Even once the cap is already full, this directory must still be
            // probed: exhausting it with zero files proves nothing was lost here,
            // and only an observed file (this directory's or a later one's) proves
            // the tree actually holds more than the cap allows.
            using var enumerator = enumerateFiles(directory).GetEnumerator();
            while (enumerator.MoveNext())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (files.Count >= maxFiles)
                {
                    return true;
                }

                files.Add(enumerator.Current);
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
        bool isRoot,
        Func<string, FileAttributes> getAttributes,
        List<string> errors)
    {
        try
        {
            var isLink = getAttributes(directory).HasFlag(FileAttributes.ReparsePoint);
            if (isLink && isRoot)
            {
                // A reparse point nested inside the tree is skipped quietly, but the
                // scan root is an explicit user action; a silent empty result would
                // look like nothing was there instead of a link that wasn't followed.
                errors.Add($"{directory}: This folder is a link and was not scanned.");
            }

            return isLink;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            errors.Add($"{directory}: {ex.Message}");
            return true;
        }
    }
}
