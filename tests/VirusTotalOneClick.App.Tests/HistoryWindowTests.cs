using VirusTotalOneClick.Core;
using VirusTotalOneClick.UI;

namespace VirusTotalOneClick.AppTests;

public class HistoryWindowTests
{
    private static UploadHistoryEntry Entry(
        string path,
        DateTimeOffset uploadedAt,
        string status = "uploaded",
        string? url = "https://www.virustotal.com/gui/file/abc",
        string submittedName = "file.bin",
        string sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
        string mode = "file")
        => new(path, submittedName, sha, uploadedAt, mode, status, url);

    [Fact]
    public void RowsAreSortedNewestFirstAtConstruction()
    {
        StaTestRunner.Run(() =>
        {
            var older = Entry(@"C:\older.txt", DateTimeOffset.UtcNow.AddDays(-2));
            var newer = Entry(@"C:\newer.txt", DateTimeOffset.UtcNow.AddHours(-1));
            var window = new HistoryWindow([older, newer]);
            using var closer = new WindowCloser(window);

            Assert.Equal(2, window.EntryCount);
            Assert.Equal(@"C:\newer.txt", window.FirstOriginalPathText);
        });
    }

    [Fact]
    public void SearchFiltersAcrossEveryFieldCaseInsensitively()
    {
        StaTestRunner.Run(() =>
        {
            var now = DateTimeOffset.UtcNow;
            var window = new HistoryWindow(
            [
                Entry(@"C:\report.pdf", now, submittedName: "scrubbed.pdf"),
                Entry(@"C:\other.txt", now.AddMinutes(-1), sha: new string('f', 64)),
                Entry(@"C:\third.txt", now.AddMinutes(-2), mode: "zip"),
            ]);
            using var closer = new WindowCloser(window);

            window.Search("SCRUBBED");
            Assert.Equal(1, window.VisibleEntryCount);
            Assert.Equal(@"C:\report.pdf", window.FirstOriginalPathText);

            window.Search("ffffffffffff");
            Assert.Equal(1, window.VisibleEntryCount);

            window.Search("ZIP");
            Assert.Equal(1, window.VisibleEntryCount);
            Assert.Equal(@"C:\third.txt", window.FirstOriginalPathText);

            window.Search("");
            Assert.Equal(3, window.VisibleEntryCount);
        });
    }

    [Fact]
    public void ASearchThatHidesTheSelectedRowClearsTheSelection()
    {
        StaTestRunner.Run(() =>
        {
            var now = DateTimeOffset.UtcNow;
            var window = new HistoryWindow(
            [
                Entry(@"C:\kept.txt", now),
                Entry(@"C:\hidden.txt", now.AddMinutes(-1)),
            ]);
            using var closer = new WindowCloser(window);

            window.ViewModel.SelectedRow = window.ViewModel.VisibleRows.Single(row => row.OriginalPath == @"C:\hidden.txt");
            window.Search("kept");

            Assert.Null(window.ViewModel.SelectedRow);
        });
    }

    [Fact]
    public void RowDisplayFormatsShaStatusAndResultLabel()
    {
        var entry = Entry(
            @"C:\a.txt",
            DateTimeOffset.UtcNow,
            status: "uploaded",
            sha: "abcdef0123456789" + new string('0', 48));
        var row = new HistoryRowViewModel(entry);

        Assert.Equal("abcdef012345...", row.Sha256Display);
        Assert.Equal("Completed", row.StatusLabel);
        Assert.Equal("Open", row.ResultLabel);
        Assert.Equal("Failed", new HistoryRowViewModel(Entry(@"C:\b.txt", DateTimeOffset.UtcNow, status: "failed")).StatusLabel);
        Assert.Equal("Queued", new HistoryRowViewModel(Entry(@"C:\c.txt", DateTimeOffset.UtcNow, status: "waiting")).StatusLabel);
        Assert.Equal("odd", new HistoryRowViewModel(Entry(@"C:\d.txt", DateTimeOffset.UtcNow, status: "odd")).StatusLabel);
        Assert.Equal(string.Empty, new HistoryRowViewModel(Entry(@"C:\e.txt", DateTimeOffset.UtcNow, url: null)).ResultLabel);
    }

    [Fact]
    public void DatesFormatRelativeToNow()
    {
        var now = new DateTimeOffset(2026, 7, 10, 15, 0, 0, TimeSpan.Zero);

        Assert.Equal("just now", HistoryRowViewModel.FormatDate(now.AddSeconds(-30), now));
        Assert.Equal("5 minutes ago", HistoryRowViewModel.FormatDate(now.AddMinutes(-5), now));
        Assert.Equal("1 minute ago", HistoryRowViewModel.FormatDate(now.AddMinutes(-1), now));
        Assert.Equal("3 hours ago", HistoryRowViewModel.FormatDate(now.AddHours(-3), now));
        Assert.Equal("Yesterday", HistoryRowViewModel.FormatDate(now.AddDays(-1), now));
        Assert.Equal("Jul 1, 2026", HistoryRowViewModel.FormatDate(new DateTimeOffset(2026, 7, 1, 12, 0, 0, now.Offset), now));
    }

    [Fact]
    public void OpenResultPrefersTheSelectedRowAndIsAllowlistGated()
    {
        StaTestRunner.Run(() =>
        {
            var launcher = new ExternalUrlLauncher(_ => { });
            var openedUrls = new List<string>();
            var now = DateTimeOffset.UtcNow;
            var window = new HistoryWindow(
                [
                    Entry(@"C:\first.txt", now, url: "https://www.virustotal.com/gui/file/first"),
                    Entry(@"C:\second.txt", now.AddMinutes(-1), url: "https://www.virustotal.com/gui/file/second"),
                ],
                openResultUrl: openedUrls.Add,
                canOpenResultUrl: launcher.CanOpen);
            using var closer = new WindowCloser(window);

            window.ViewModel.OpenResultCommand.Execute(null);
            Assert.Equal(["https://www.virustotal.com/gui/file/first"], openedUrls);

            window.ViewModel.SelectedRow = window.ViewModel.VisibleRows[1];
            window.ViewModel.OpenResultCommand.Execute(null);
            Assert.Equal("https://www.virustotal.com/gui/file/second", openedUrls[^1]);

            var disallowed = new HistoryWindow(
                [Entry(@"C:\bad.txt", now, url: "https://evil.example/x")],
                openResultUrl: openedUrls.Add,
                canOpenResultUrl: launcher.CanOpen);
            using var closerB = new WindowCloser(disallowed);

            Assert.False(disallowed.ViewModel.OpenResultCommand.CanExecute(null));

            var noCallback = new HistoryWindow([Entry(@"C:\a.txt", now)]);
            using var closerC = new WindowCloser(noCallback);
            Assert.False(noCallback.ViewModel.OpenResultCommand.CanExecute(null));
        });
    }

    [Fact]
    public void ClearHistoryInvokesTheCallbackAndEmptiesRows()
    {
        StaTestRunner.Run(() =>
        {
            var cleared = false;
            var window = new HistoryWindow(
                [Entry(@"C:\a.txt", DateTimeOffset.UtcNow)],
                clearHistory: () => cleared = true);
            using var closer = new WindowCloser(window);

            window.ClearHistory();

            Assert.True(cleared);
            Assert.Equal(0, window.EntryCount);
            Assert.Equal(0, window.VisibleEntryCount);
            Assert.True(window.ViewModel.IsEmpty);
        });
    }

    [Fact]
    public void ClearHistoryWithALockedFileReportsFailureWithoutThrowing()
    {
        StaTestRunner.Run(() =>
        {
            var window = new HistoryWindow(
                [Entry(@"C:\a.txt", DateTimeOffset.UtcNow)],
                clearHistory: () => throw new IOException("history.jsonl is in use by another process"));
            using var closer = new WindowCloser(window);

            var exception = Record.Exception(() => window.ClearHistory());

            Assert.Null(exception);
            Assert.Equal(1, window.EntryCount);
            Assert.StartsWith("Could not clear history", window.ViewModel.CaptionText, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void SearchAfterClearDoesNotResurrectClearedRows()
    {
        StaTestRunner.Run(() =>
        {
            var window = new HistoryWindow(
                [Entry(@"C:\findme.txt", DateTimeOffset.UtcNow)],
                clearHistory: () => { });
            using var closer = new WindowCloser(window);

            window.ClearHistory();
            window.Search("findme");

            Assert.Equal(0, window.VisibleEntryCount);
        });
    }

    [Fact]
    public void TheEmptyStateAndCaptionRender()
    {
        StaTestRunner.Run(() =>
        {
            var empty = new HistoryWindow([]);
            using var closerA = new WindowCloser(empty);

            VisualTreeAssert.AssertWindowContainsText(empty, "Upload history");
            VisualTreeAssert.AssertWindowContainsText(empty, "No uploads yet.");
            VisualTreeAssert.AssertWindowContainsText(empty, "Files uploaded through the Explorer action will appear here.");
            VisualTreeAssert.AssertWindowContainsText(empty, "Local history is enabled.");

            var disabled = new HistoryWindow([], keepHistoryEnabled: false);
            using var closerB = new WindowCloser(disabled);
            VisualTreeAssert.AssertWindowContainsText(disabled, "Local history is disabled.");
        });
    }
}
