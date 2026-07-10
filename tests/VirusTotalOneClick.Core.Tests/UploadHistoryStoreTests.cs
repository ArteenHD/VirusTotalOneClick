using System.Globalization;
using System.Text.Json;

namespace VirusTotalOneClick.Core.Tests;

public class UploadHistoryStoreTests
{
    [Fact]
    public void AppendThenReadAllRoundTripsEntries()
    {
        using var folder = new TemporaryFolder();
        var store = new UploadHistoryStore(Path.Combine(folder.Path, "history.jsonl"));
        var first = Entry("first");
        var second = Entry("second") with { Mode = "zip", Status = "failed", ResultUrl = "https://www.virustotal.com/gui/file-analysis/xyz" };

        store.Append(first);
        store.Append(second);

        Assert.Equal(new[] { first, second }, store.ReadAll());
    }

    [Fact]
    public void EntriesArePersistedAsOneCompactJsonObjectPerLine()
    {
        using var folder = new TemporaryFolder();
        var path = Path.Combine(folder.Path, "history.jsonl");
        var store = new UploadHistoryStore(path);

        store.Append(Entry("first"));
        store.Append(Entry("second"));

        var lines = File.ReadAllLines(path);
        Assert.Equal(2, lines.Length);
        foreach (var line in lines)
        {
            using var document = JsonDocument.Parse(line);
            Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
            Assert.DoesNotContain("\n", line, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AppendCreatesTheParentDirectoryWhenMissing()
    {
        using var folder = new TemporaryFolder();
        var path = Path.Combine(folder.Path, "nested", "history.jsonl");

        new UploadHistoryStore(path).Append(Entry("first"));

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void ReadAllReturnsEmptyWhenTheFileDoesNotExist()
    {
        using var folder = new TemporaryFolder();

        Assert.Empty(new UploadHistoryStore(Path.Combine(folder.Path, "history.jsonl")).ReadAll());
    }

    [Fact]
    public void CorruptLinesAreSkippedWhileValidLinesSurvive()
    {
        using var folder = new TemporaryFolder();
        var path = Path.Combine(folder.Path, "history.jsonl");
        var store = new UploadHistoryStore(path);
        store.Append(Entry("kept"));
        File.AppendAllText(path, "{ this line was torn mid-write" + Environment.NewLine);
        store.Append(Entry("also-kept"));

        var entries = store.ReadAll();

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, entry => entry.SubmittedName == "kept.bin");
        Assert.Contains(entries, entry => entry.SubmittedName == "also-kept.bin");
    }

    [Fact]
    public void BlankLinesAreIgnored()
    {
        using var folder = new TemporaryFolder();
        var path = Path.Combine(folder.Path, "history.jsonl");
        var store = new UploadHistoryStore(path);
        store.Append(Entry("first"));
        File.AppendAllText(path, Environment.NewLine + "   " + Environment.NewLine);
        store.Append(Entry("second"));

        Assert.Equal(2, store.ReadAll().Count);
    }

    [Fact]
    public void ClearDeletesTheFileAndSubsequentReadsAreEmpty()
    {
        using var folder = new TemporaryFolder();
        var path = Path.Combine(folder.Path, "history.jsonl");
        var store = new UploadHistoryStore(path);
        store.Append(Entry("gone"));

        store.Clear();

        Assert.False(File.Exists(path));
        Assert.Empty(store.ReadAll());
        store.Clear();
    }

    [Fact]
    public void AppendRetriesWhileAnotherWriterHoldsTheFile()
    {
        using var folder = new TemporaryFolder();
        var path = Path.Combine(folder.Path, "history.jsonl");
        var store = new UploadHistoryStore(path);
        store.Append(Entry("first"));

        Task appendTask;
        using (var exclusiveLock = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            appendTask = Task.Run(() => store.Append(Entry("second")));
            Thread.Sleep(120);
        }

        Assert.True(appendTask.Wait(TimeSpan.FromSeconds(5)));
        Assert.Equal(2, store.ReadAll().Count);
    }

    [Fact]
    public void AppendGivesUpWhenTheFileStaysLockedPastTheRetryBudget()
    {
        using var folder = new TemporaryFolder();
        var path = Path.Combine(folder.Path, "history.jsonl");
        var store = new UploadHistoryStore(path);
        store.Append(Entry("first"));

        using var exclusiveLock = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var exception = Record.Exception(() => store.Append(Entry("blocked")));

        Assert.IsAssignableFrom<IOException>(exception);
    }

    [Fact]
    public void ManyConcurrentWritersAllLandTheirEntries()
    {
        using var folder = new TemporaryFolder();
        var path = Path.Combine(folder.Path, "history.jsonl");

        Parallel.For(0, 4, writer =>
        {
            var store = new UploadHistoryStore(path);
            for (var item = 0; item < 5; item++)
            {
                store.Append(Entry($"writer{writer}-item{item}"));
            }
        });

        var entries = new UploadHistoryStore(path).ReadAll();
        Assert.Equal(20, entries.Count);
        Assert.Equal(20, entries.Select(entry => entry.SubmittedName).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void NullStoreWritesNothingReadsEmptyAndClearsQuietly()
    {
        var store = NullUploadHistoryStore.Instance;

        store.Append(Entry("ignored"));
        store.Clear();

        Assert.Empty(store.ReadAll());
        Assert.Same(NullUploadHistoryStore.Instance, store);
    }

    private static UploadHistoryEntry Entry(string marker) => new(
        OriginalPath: $@"C:\samples\{marker}.bin",
        SubmittedName: $"{marker}.bin",
        Sha256: new string('a', 64),
        UploadedAtUtc: DateTimeOffset.Parse("2026-07-10T12:00:00Z", CultureInfo.InvariantCulture),
        Mode: "file",
        Status: "uploaded",
        ResultUrl: null);
}
