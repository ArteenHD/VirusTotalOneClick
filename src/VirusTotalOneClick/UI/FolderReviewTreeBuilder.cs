using System.IO;

namespace VirusTotalOneClick.UI;

internal static class FolderReviewTreeBuilder
{
    public static FolderReviewNode? Build(string root, IReadOnlyList<string> files)
    {
        if (string.IsNullOrEmpty(root))
        {
            return null;
        }

        var rootName = string.IsNullOrEmpty(Path.GetFileName(root)) ? root : Path.GetFileName(root);
        var rootNode = new FolderReviewNode(rootName, root, isDirectory: true)
        {
            IsExpanded = true,
        };

        var directories = new Dictionary<string, FolderReviewNode>(StringComparer.OrdinalIgnoreCase)
        {
            [Normalize(root)] = rootNode,
        };

        foreach (var file in files)
        {
            if (string.IsNullOrWhiteSpace(file))
            {
                continue;
            }

            var directory = Path.GetDirectoryName(file) ?? root;
            var parent = EnsureDirectory(directory, root, rootNode, directories);
            var fileNode = new FolderReviewNode(Path.GetFileName(file), file, isDirectory: false, TryGetFileSize(file))
            {
                Parent = parent,
            };
            parent.Children.Add(fileNode);
        }

        return rootNode;
    }

    private static FolderReviewNode EnsureDirectory(
        string directory,
        string root,
        FolderReviewNode rootNode,
        Dictionary<string, FolderReviewNode> cache)
    {
        var normalized = Normalize(directory);
        if (cache.TryGetValue(normalized, out var existing))
        {
            return existing;
        }

        var rootNormalized = Normalize(root);
        if (!normalized.StartsWith(rootNormalized, StringComparison.OrdinalIgnoreCase))
        {
            return rootNode;
        }

        var parentDirectory = Path.GetDirectoryName(directory) ?? root;
        var parentNode = string.Equals(Normalize(parentDirectory), rootNormalized, StringComparison.OrdinalIgnoreCase)
            ? rootNode
            : EnsureDirectory(parentDirectory, root, rootNode, cache);

        var name = Path.GetFileName(directory);
        if (string.IsNullOrEmpty(name))
        {
            return rootNode;
        }

        var node = new FolderReviewNode(name, directory, isDirectory: true)
        {
            Parent = parentNode,
            IsExpanded = true,
        };
        parentNode.Children.Add(node);
        cache[normalized] = node;
        return node;
    }

    private static string Normalize(string path)
        => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static long? TryGetFileSize(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? info.Length : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
