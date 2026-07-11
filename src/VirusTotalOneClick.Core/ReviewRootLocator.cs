namespace VirusTotalOneClick.Core;

public static class ReviewRootLocator
{
    /// <summary>
    /// Finds the deepest directory that contains every selection: directories
    /// count as themselves, files as their parent. Selections that share nothing
    /// beyond the drive collapse to the first selection's drive root.
    /// </summary>
    public static string FindCommonRoot(IReadOnlyList<FileSelection> selections)
    {
        var directories = selections.Select(DirectoryOf).ToList();
        if (directories.Count == 0)
        {
            return string.Empty;
        }

        var commonParts = SplitSegments(directories[0]);
        foreach (var directory in directories.Skip(1))
        {
            var parts = SplitSegments(directory);
            var sharedLength = 0;
            var limit = Math.Min(commonParts.Length, parts.Length);
            while (sharedLength < limit &&
                string.Equals(commonParts[sharedLength], parts[sharedLength], StringComparison.OrdinalIgnoreCase))
            {
                sharedLength++;
            }

            commonParts = commonParts.Take(sharedLength).ToArray();
            if (commonParts.Length == 0)
            {
                return Path.GetPathRoot(directories[0]) ?? directories[0];
            }
        }

        var root = string.Join(Path.DirectorySeparatorChar, commonParts);
        if (root.EndsWith(':'))
        {
            root += Path.DirectorySeparatorChar;
        }

        return string.IsNullOrWhiteSpace(root) ? directories[0] : root;
    }

    private static string DirectoryOf(FileSelection selection)
    {
        return selection.IsDirectory
            ? Path.GetFullPath(selection.Path)
            : Path.GetDirectoryName(Path.GetFullPath(selection.Path))
                ?? Path.GetPathRoot(selection.Path)
                ?? selection.Path;
    }

    private static string[] SplitSegments(string path)
    {
        return path
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
