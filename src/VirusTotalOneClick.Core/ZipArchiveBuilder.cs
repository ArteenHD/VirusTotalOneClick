using System.IO.Compression;

namespace VirusTotalOneClick.Core;

public static class ZipArchiveBuilder
{
    public static void Build(ZipPlan plan, CancellationToken cancellationToken = default)
        => Build(plan, entryWritten: null, cancellationToken);

    internal static void Build(ZipPlan plan, Action<string>? entryWritten, CancellationToken cancellationToken = default)
    {
        if (File.Exists(plan.DestinationZipPath))
        {
            File.Delete(plan.DestinationZipPath);
        }

        using var archive = ZipFile.Open(plan.DestinationZipPath, ZipArchiveMode.Create);
        var commonRoot = FindCommonDirectory(plan.SourceFiles);
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < plan.SourceFiles.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var source = plan.SourceFiles[index];
            var entryName = plan.ScrubInternalPaths
                ? CreateScrubbedEntryName(source, index + 1)
                : CreateRelativeEntryName(commonRoot, source);
            var uniqueName = EnsureUniqueEntryName(entryName, usedNames);

            archive.CreateEntryFromFile(source, uniqueName, CompressionLevel.SmallestSize);
            entryWritten?.Invoke(uniqueName);
        }
    }

    private static string EnsureUniqueEntryName(string entryName, HashSet<string> usedNames)
    {
        if (usedNames.Add(entryName))
        {
            return entryName;
        }

        var directory = Path.GetDirectoryName(entryName)?.Replace(Path.DirectorySeparatorChar, '/') ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(entryName);
        var extension = Path.GetExtension(entryName);
        for (var suffix = 2; ; suffix++)
        {
            var candidate = string.IsNullOrEmpty(directory)
                ? $"{fileName} ({suffix}){extension}"
                : $"{directory}/{fileName} ({suffix}){extension}";
            if (usedNames.Add(candidate))
            {
                return candidate;
            }
        }
    }

    private static string CreateScrubbedEntryName(string source, int number)
    {
        var extension = Path.GetExtension(source);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".bin";
        }

        return $"file-{number:000}{extension.ToLowerInvariant()}";
    }

    private static string CreateRelativeEntryName(string? commonRoot, string source)
    {
        var entryName = string.IsNullOrWhiteSpace(commonRoot)
            ? Path.GetFileName(source)
            : Path.GetRelativePath(commonRoot, source);

        entryName = entryName.Replace(Path.DirectorySeparatorChar, '/');
        if (Path.AltDirectorySeparatorChar != Path.DirectorySeparatorChar)
        {
            entryName = entryName.Replace(Path.AltDirectorySeparatorChar, '/');
        }

        return string.IsNullOrWhiteSpace(entryName) || entryName.StartsWith("..", StringComparison.Ordinal)
            ? Path.GetFileName(source)
            : entryName;
    }

    private static string? FindCommonDirectory(IReadOnlyList<string> sourceFiles)
    {
        var directories = sourceFiles
            .Select(Path.GetDirectoryName)
            .Where(directory => !string.IsNullOrWhiteSpace(directory))
            .Select(directory => directory!)
            .ToList();

        if (directories.Count == 0)
        {
            return null;
        }

        var common = directories[0];
        foreach (var directory in directories.Skip(1))
        {
            common = FindCommonDirectory(common, directory);
        }

        return common;
    }

    private static string FindCommonDirectory(string left, string right)
    {
        var leftParts = left.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var rightParts = right.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var count = Math.Min(leftParts.Length, rightParts.Length);
        var commonParts = new List<string>();

        for (var index = 0; index < count; index++)
        {
            if (!string.Equals(leftParts[index], rightParts[index], StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            commonParts.Add(leftParts[index]);
        }

        if (commonParts.Count == 0)
        {
            return string.Empty;
        }

        var joined = string.Join(Path.DirectorySeparatorChar, commonParts);

        // A bare drive token like "C:" resolves relative paths against the drive's
        // current working directory, not the drive root; force the root form.
        if (joined.Length == 2 && joined.EndsWith(':'))
        {
            joined += Path.DirectorySeparatorChar;
        }

        return joined;
    }
}
