namespace VirusTotalOneClick.Core.Tests;

public class FolderScannerTests
{
    [Fact]
    public void ScanReturnsFilesAcrossNestedDirectories()
    {
        using var folder = new TemporaryFolder();
        var fileA = folder.CreateFile("a.txt");
        var fileB = folder.CreateFile(Path.Combine("nested", "b.txt"));
        var fileC = folder.CreateFile(Path.Combine("nested", "deeper", "c.txt"));

        var result = FolderScanner.Scan(folder.Path, new FolderScanOptions(MaxFiles: 100));

        Assert.False(result.Truncated);
        Assert.Empty(result.Errors);
        Assert.Equal(
            new[] { fileA, fileB, fileC }.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray(),
            result.Files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    [Fact]
    public void ScanTruncatesAtMaxFiles()
    {
        using var folder = new TemporaryFolder();
        for (var i = 0; i < 5; i++)
        {
            folder.CreateFile($"file-{i}.txt");
        }

        var result = FolderScanner.Scan(folder.Path, new FolderScanOptions(MaxFiles: 3));

        Assert.True(result.Truncated);
        Assert.Equal(3, result.Files.Count);
    }

    [Fact]
    public void NonPositiveMaxFilesReturnsTruncatedWithoutEnumeratingAnything()
    {
        var touched = false;

        var result = FolderScanner.Scan(
            @"C:\never-visited",
            new FolderScanOptions(MaxFiles: 0),
            _ => { touched = true; return []; },
            _ => { touched = true; return []; },
            _ => { touched = true; return FileAttributes.Directory; });

        Assert.True(result.Truncated);
        Assert.Empty(result.Files);
        Assert.False(touched);
    }

    [Fact]
    public void ScanStopsPullingFromLazyEnumerationTheMomentMaxFilesIsReached()
    {
        var pulled = 0;

        IEnumerable<string> LazyFiles()
        {
            for (var i = 0; i < 1000; i++)
            {
                pulled++;
                yield return $@"C:\root\file-{i}.txt";
            }
        }

        var result = FolderScanner.Scan(
            @"C:\root",
            new FolderScanOptions(MaxFiles: 1),
            _ => LazyFiles(),
            _ => [],
            _ => FileAttributes.Directory);

        Assert.True(result.Truncated);
        Assert.Single(result.Files);
        Assert.Equal(1, pulled);
    }

    [Fact]
    public void TruncationKeepsTheFileFromTheFirstDirectoryInBreadthFirstOrder()
    {
        var filesByDirectory = new Dictionary<string, string[]>
        {
            [@"C:\root"] = [],
            [@"C:\root\alpha"] = [@"C:\root\alpha\first.txt"],
            [@"C:\root\beta"] = [@"C:\root\beta\second.txt"],
        };
        var directoriesByDirectory = new Dictionary<string, string[]>
        {
            [@"C:\root"] = [@"C:\root\alpha", @"C:\root\beta"],
            [@"C:\root\alpha"] = [],
            [@"C:\root\beta"] = [],
        };

        var result = FolderScanner.Scan(
            @"C:\root",
            new FolderScanOptions(MaxFiles: 1),
            directory => filesByDirectory[directory],
            directory => directoriesByDirectory[directory],
            _ => FileAttributes.Directory);

        Assert.True(result.Truncated);
        Assert.Equal(new[] { @"C:\root\alpha\first.txt" }, result.Files);
    }

    [Fact]
    public void MissingRootYieldsAnErrorAndNoFiles()
    {
        using var folder = new TemporaryFolder();
        var missing = Path.Combine(folder.Path, "does-not-exist");

        var result = FolderScanner.Scan(missing, new FolderScanOptions(MaxFiles: 10));

        Assert.False(result.Truncated);
        Assert.Empty(result.Files);
        Assert.Single(result.Errors);
        Assert.Contains(missing, result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InaccessibleSubdirectoryDoesNotAbortTheScan()
    {
        var result = FolderScanner.Scan(
            @"C:\root",
            new FolderScanOptions(MaxFiles: 10),
            directory => directory == @"C:\root\locked"
                ? throw new UnauthorizedAccessException("Access to the path is denied.")
                : [$@"{directory}\file.txt"],
            directory => directory == @"C:\root" ? [@"C:\root\locked", @"C:\root\open"] : [],
            _ => FileAttributes.Directory);

        Assert.Equal(2, result.Files.Count);
        Assert.Contains(@"C:\root\open\file.txt", result.Files);
        Assert.Single(result.Errors);
        Assert.Contains(@"C:\root\locked", result.Errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public void ReparsePointSubdirectoriesAreSkippedNotRecursed()
    {
        var enumeratedLink = false;

        var result = FolderScanner.Scan(
            @"C:\root",
            new FolderScanOptions(MaxFiles: 10),
            directory =>
            {
                if (directory == @"C:\root\link")
                {
                    enumeratedLink = true;
                }

                return [$@"{directory}\file.txt"];
            },
            directory => directory == @"C:\root" ? [@"C:\root\link"] : [],
            path => path == @"C:\root\link"
                ? FileAttributes.Directory | FileAttributes.ReparsePoint
                : FileAttributes.Directory);

        Assert.False(enumeratedLink);
        Assert.Single(result.Files);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ReparsePointRootIsSkippedWithoutEnumeratingItsFiles()
    {
        var enumerated = false;

        var result = FolderScanner.Scan(
            @"C:\link-root",
            new FolderScanOptions(MaxFiles: 10),
            _ => { enumerated = true; return [@"C:\link-root\file.txt"]; },
            _ => { enumerated = true; return []; },
            _ => FileAttributes.Directory | FileAttributes.ReparsePoint);

        Assert.False(enumerated);
        Assert.Empty(result.Files);
        Assert.False(result.Truncated);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeAnyEnumeration()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var touched = false;

        Assert.Throws<OperationCanceledException>(() => FolderScanner.Scan(
            @"C:\root",
            new FolderScanOptions(MaxFiles: 10),
            _ => { touched = true; return []; },
            _ => { touched = true; return []; },
            _ => { touched = true; return FileAttributes.Directory; },
            cts.Token));

        Assert.False(touched);
    }

    [Fact]
    public void CancellationDuringEnumerationStopsPromptly()
    {
        using var cts = new CancellationTokenSource();
        var pulled = 0;

        IEnumerable<string> Files()
        {
            for (var i = 0; i < 1000; i++)
            {
                pulled++;
                if (pulled == 1)
                {
                    cts.Cancel();
                }

                yield return $@"C:\root\file-{i}.txt";
            }
        }

        Assert.Throws<OperationCanceledException>(() => FolderScanner.Scan(
            @"C:\root",
            new FolderScanOptions(MaxFiles: 100),
            _ => Files(),
            _ => [],
            _ => FileAttributes.Directory,
            cts.Token));

        Assert.Equal(1, pulled);
    }
}
