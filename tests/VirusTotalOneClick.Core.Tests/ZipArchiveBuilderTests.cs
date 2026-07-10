using System.IO.Compression;

namespace VirusTotalOneClick.Core.Tests;

public class ZipArchiveBuilderTests
{
    [Fact]
    public void ScrubbedEntriesUseSequentialNumberedNamesWithLowercaseExtensions()
    {
        using var folder = new TemporaryFolder();
        var doc = folder.CreateFile("Report.PDF", "pdf bytes");
        var tool = folder.CreateFile(Path.Combine("nested", "Tool.exe"), "exe bytes");
        var readme = folder.CreateFile("README", "no extension");
        var destination = Path.Combine(folder.Path, "out.zip");

        ZipArchiveBuilder.Build(new ZipPlan([doc, tool, readme], destination, ScrubInternalPaths: true));

        using var archive = ZipFile.OpenRead(destination);
        Assert.Equal(
            new[] { "file-001.pdf", "file-002.exe", "file-003.bin" },
            archive.Entries.Select(entry => entry.FullName).ToArray());
    }

    [Fact]
    public void UnscrubbedSingleFileUsesItsBareName()
    {
        using var folder = new TemporaryFolder();
        var file = folder.CreateFile("sample.txt", "text");
        var destination = Path.Combine(folder.Path, "out.zip");

        ZipArchiveBuilder.Build(new ZipPlan([file], destination, ScrubInternalPaths: false));

        using var archive = ZipFile.OpenRead(destination);
        Assert.Equal(new[] { "sample.txt" }, archive.Entries.Select(entry => entry.FullName).ToArray());
    }

    [Fact]
    public void UnscrubbedEntriesPreserveRelativePathsWithForwardSlashes()
    {
        using var folder = new TemporaryFolder();
        var top = folder.CreateFile("sample.txt", "top");
        var nested = folder.CreateFile(Path.Combine("nested", "sample.dll"), "nested");
        var destination = Path.Combine(folder.Path, "out.zip");

        ZipArchiveBuilder.Build(new ZipPlan([top, nested], destination, ScrubInternalPaths: false));

        using var archive = ZipFile.OpenRead(destination);
        Assert.Equal(
            new[] { "sample.txt", "nested/sample.dll" },
            archive.Entries.Select(entry => entry.FullName).ToArray());
    }

    [Fact]
    public void SameNamedFilesInDifferentFoldersStayDistinctByRelativePath()
    {
        using var folder = new TemporaryFolder();
        var first = folder.CreateFile(Path.Combine("first", "readme.txt"), "one");
        var second = folder.CreateFile(Path.Combine("second", "readme.txt"), "two");
        var destination = Path.Combine(folder.Path, "out.zip");

        ZipArchiveBuilder.Build(new ZipPlan([first, second], destination, ScrubInternalPaths: false));

        using var archive = ZipFile.OpenRead(destination);
        Assert.Equal(
            new[] { "first/readme.txt", "second/readme.txt" },
            archive.Entries.Select(entry => entry.FullName).ToArray());
    }

    [Fact]
    public void CollidingEntryNamesGetNumberedSuffixes()
    {
        using var folder = new TemporaryFolder();
        var file = folder.CreateFile("readme.txt", "same file three times");
        var destination = Path.Combine(folder.Path, "out.zip");

        ZipArchiveBuilder.Build(new ZipPlan([file, file, file], destination, ScrubInternalPaths: false));

        using var archive = ZipFile.OpenRead(destination);
        Assert.Equal(
            new[] { "readme.txt", "readme (2).txt", "readme (3).txt" },
            archive.Entries.Select(entry => entry.FullName).ToArray());
    }

    [Fact]
    public void CollidingNestedEntryNamesKeepTheirDirectoryPrefix()
    {
        using var folder = new TemporaryFolder();
        var anchor = folder.CreateFile("anchor.txt", "keeps the common root above nested");
        var nested = folder.CreateFile(Path.Combine("nested", "readme.txt"), "dup");
        var destination = Path.Combine(folder.Path, "out.zip");

        ZipArchiveBuilder.Build(new ZipPlan([anchor, nested, nested], destination, ScrubInternalPaths: false));

        using var archive = ZipFile.OpenRead(destination);
        Assert.Equal(
            new[] { "anchor.txt", "nested/readme.txt", "nested/readme (2).txt" },
            archive.Entries.Select(entry => entry.FullName).ToArray());
    }

    [Fact]
    public void CollisionsAreDetectedCaseInsensitively()
    {
        using var folder = new TemporaryFolder();
        var anchor = folder.CreateFile("anchor.txt", "keeps the common root above nested");
        var nested = folder.CreateFile(Path.Combine("nested", "readme.txt"), "dup");
        var upperCased = nested.Replace(
            Path.Combine("nested", "readme.txt"),
            Path.Combine("NESTED", "README.TXT"),
            StringComparison.Ordinal);
        var destination = Path.Combine(folder.Path, "out.zip");

        ZipArchiveBuilder.Build(new ZipPlan([anchor, nested, upperCased], destination, ScrubInternalPaths: false));

        using var archive = ZipFile.OpenRead(destination);
        Assert.Equal(
            new[] { "anchor.txt", "nested/readme.txt", "NESTED/README (2).TXT" },
            archive.Entries.Select(entry => entry.FullName).ToArray());
    }

    [Fact]
    public void AnExistingDestinationZipIsReplaced()
    {
        using var folder = new TemporaryFolder();
        var file = folder.CreateFile("sample.txt", "fresh");
        var destination = folder.CreateFile("out.zip", "this is not a zip");

        ZipArchiveBuilder.Build(new ZipPlan([file], destination, ScrubInternalPaths: false));

        using var archive = ZipFile.OpenRead(destination);
        Assert.Single(archive.Entries);
    }

    [Fact]
    public void PreCanceledTokenThrowsBeforeAnyEntryIsAdded()
    {
        using var folder = new TemporaryFolder();
        var file = folder.CreateFile("sample.txt", "text");
        var destination = Path.Combine(folder.Path, "out.zip");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => ZipArchiveBuilder.Build(new ZipPlan([file], destination, ScrubInternalPaths: false), cts.Token));

        using var archive = ZipFile.OpenRead(destination);
        Assert.Empty(archive.Entries);
    }

    [Fact]
    public void CancellationMidBuildStopsBeforeRemainingEntriesAndLeavesAReadableArchive()
    {
        using var folder = new TemporaryFolder();
        var first = folder.CreateFile("first.txt", "one");
        var second = folder.CreateFile("second.txt", "two");
        var third = folder.CreateFile("third.txt", "three");
        var destination = Path.Combine(folder.Path, "out.zip");
        using var cts = new CancellationTokenSource();

        var plan = new ZipPlan([first, second, third], destination, ScrubInternalPaths: false);

        Assert.Throws<OperationCanceledException>(
            () => ZipArchiveBuilder.Build(plan, entryWritten: _ => cts.Cancel(), cts.Token));

        using (var archive = ZipFile.OpenRead(destination))
        {
            Assert.Single(archive.Entries);
        }

        File.Delete(destination);
        Assert.False(File.Exists(destination));
    }
}
