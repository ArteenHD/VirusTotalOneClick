using System.Security.Cryptography;

namespace VirusTotalOneClick.Core.Tests;

public class UploadSessionTests
{
    private static AnonymizedUploadItem Item(string path) => new(path, path, Path.GetFileName(path));

    private static string Sha256Of(string content)
        => Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content)));

    [Fact]
    public async Task ItemsUploadSequentiallyAndEachOneLandsInHistory()
    {
        using var folder = new TemporaryFolder();
        var first = folder.CreateFile("first.txt", "first content");
        var second = folder.CreateFile("second.txt", "second content");
        using var client = new FakeVirusTotalClient();
        var history = new RecordingHistoryStore();
        var session = new UploadSession(client, history);

        var result = await session.RunAsync([Item(first), Item(second)], UploadMode.File, CancellationToken.None);

        Assert.Equal(2, result.ItemCount);
        Assert.Equal(2, result.UploadedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.False(result.IsCanceled);
        Assert.Equal([first, second], client.Uploads.Select(upload => upload.FilePath));
        Assert.All(result.ItemResults, item => Assert.Equal("uploaded", item.Status));
        Assert.All(result.ItemResults, item => Assert.NotNull(item.ResultUrl));
        Assert.Equal(2, history.Appended.Count);
        Assert.All(history.Appended, entry => Assert.Equal("file", entry.Mode));
        Assert.All(history.Appended, entry => Assert.Equal("uploaded", entry.Status));
        Assert.Equal(Sha256Of("first content"), history.Appended[0].Sha256);
    }

    [Fact]
    public async Task TemporaryUploadCopyIsDeletedAndTheOriginalSurvives()
    {
        using var folder = new TemporaryFolder();
        var original = folder.CreateFile("document.txt", "shared content");
        var uploadCopy = folder.CreateFile("upload-copy.tmp", "shared content");
        using var client = new FakeVirusTotalClient();
        var history = new RecordingHistoryStore();
        var session = new UploadSession(client, history);

        await session.RunAsync(
            [new AnonymizedUploadItem(original, uploadCopy, "sample-deadbeef.txt")],
            UploadMode.File,
            CancellationToken.None);

        Assert.False(File.Exists(uploadCopy));
        Assert.True(File.Exists(original));
        Assert.Equal(Sha256Of("shared content"), Assert.Single(history.Appended).Sha256);
    }

    [Fact]
    public async Task AFailedItemDoesNotStopLaterItemsAndItsMessageFlowsThrough()
    {
        using var folder = new TemporaryFolder();
        var first = folder.CreateFile("first.txt", "a");
        var second = folder.CreateFile("second.txt", "b");
        using var client = new FakeVirusTotalClient { FailFirstUpload = true };
        var history = new RecordingHistoryStore();
        var session = new UploadSession(client, history);

        var result = await session.RunAsync([Item(first), Item(second)], UploadMode.File, CancellationToken.None);

        Assert.Equal(1, result.UploadedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.False(result.IsCanceled);
        Assert.Equal("failed", result.ItemResults[0].Status);
        Assert.Equal("No quota left.", result.ItemResults[0].ErrorMessage);
        Assert.Equal("uploaded", result.ItemResults[1].Status);
        Assert.Equal(["failed", "uploaded"], history.Appended.Select(entry => entry.Status));
        Assert.Null(history.Appended[0].ResultUrl);
    }

    [Fact]
    public async Task HistoryFailureAfterASuccessfulUploadNeverDowngradesTheResult()
    {
        using var folder = new TemporaryFolder();
        var file = folder.CreateFile("file.txt", "content");
        using var client = new FakeVirusTotalClient();
        var session = new UploadSession(client, new ThrowingHistoryStore());

        var result = await session.RunAsync([Item(file)], UploadMode.File, CancellationToken.None);

        var item = Assert.Single(result.ItemResults);
        Assert.Equal("uploaded", item.Status);
        Assert.NotNull(item.ResultUrl);
    }

    [Fact]
    public async Task HistoryFailureAfterAFailedUploadKeepsTheOriginalError()
    {
        using var folder = new TemporaryFolder();
        var file = folder.CreateFile("file.txt", "content");
        using var client = new FakeVirusTotalClient { FailFirstUpload = true };
        var session = new UploadSession(client, new ThrowingHistoryStore());

        var result = await session.RunAsync([Item(file)], UploadMode.File, CancellationToken.None);

        var item = Assert.Single(result.ItemResults);
        Assert.Equal("failed", item.Status);
        Assert.Equal("No quota left.", item.ErrorMessage);
    }

    [Fact]
    public async Task AnUnresolvableHashSkipsTheHistoryEntryButTheUploadStaysUploaded()
    {
        using var folder = new TemporaryFolder();
        var file = folder.CreateFile("file.txt", "content");
        using var client = new FakeVirusTotalClient { DeleteUploadedFileBeforeReturning = true };
        var history = new RecordingHistoryStore();
        var session = new UploadSession(client, history);

        var result = await session.RunAsync([Item(file)], UploadMode.File, CancellationToken.None);

        Assert.Equal("uploaded", Assert.Single(result.ItemResults).Status);
        Assert.Empty(history.Appended);
    }

    [Fact]
    public async Task ALockedTemporaryCopyStillReportsUploaded()
    {
        using var folder = new TemporaryFolder();
        var original = folder.CreateFile("original.txt", "content");
        var uploadCopy = folder.CreateFile("upload-copy.tmp", "content");
        using var client = new FakeVirusTotalClient { LockUploadedFileBeforeReturning = true };
        var session = new UploadSession(client, new RecordingHistoryStore());

        var result = await session.RunAsync(
            [new AnonymizedUploadItem(original, uploadCopy, "sample-deadbeef.txt")],
            UploadMode.File,
            CancellationToken.None);

        Assert.Equal("uploaded", Assert.Single(result.ItemResults).Status);
        Assert.True(File.Exists(uploadCopy));
    }

    [Fact]
    public async Task ARateLimitedUploadRetriesOnceAfterTheDefaultDelay()
    {
        using var folder = new TemporaryFolder();
        var file = folder.CreateFile("file.txt", "content");
        using var client = new FakeVirusTotalClient { RateLimitedCallCount = 1 };
        var delays = new List<TimeSpan>();
        var session = new UploadSession(
            client,
            new RecordingHistoryStore(),
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });

        var result = await session.RunAsync([Item(file)], UploadMode.File, CancellationToken.None);

        Assert.Equal([TimeSpan.FromSeconds(20)], delays);
        Assert.Equal(2, client.CallCount);
        Assert.Equal("uploaded", Assert.Single(result.ItemResults).Status);
    }

    [Fact]
    public async Task RetryAfterFromTheApiOverridesTheDefaultDelay()
    {
        using var folder = new TemporaryFolder();
        var file = folder.CreateFile("file.txt", "content");
        using var client = new FakeVirusTotalClient
        {
            RateLimitedCallCount = 1,
            RateLimitRetryAfter = TimeSpan.FromSeconds(5),
        };
        var delays = new List<TimeSpan>();
        var session = new UploadSession(
            client,
            new RecordingHistoryStore(),
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });

        await session.RunAsync([Item(file)], UploadMode.File, CancellationToken.None);

        Assert.Equal([TimeSpan.FromSeconds(5)], delays);
    }

    [Fact]
    public async Task ASecondRateLimitGivesUpInsteadOfLooping()
    {
        using var folder = new TemporaryFolder();
        var file = folder.CreateFile("file.txt", "content");
        using var client = new FakeVirusTotalClient { RateLimitedCallCount = 2 };
        var delays = new List<TimeSpan>();
        var history = new RecordingHistoryStore();
        var session = new UploadSession(
            client,
            history,
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });

        var result = await session.RunAsync([Item(file)], UploadMode.File, CancellationToken.None);

        Assert.Single(delays);
        Assert.Equal(2, client.CallCount);
        var item = Assert.Single(result.ItemResults);
        Assert.Equal("failed", item.Status);
        Assert.Equal("Rate limit exceeded.", item.ErrorMessage);
        Assert.Equal("failed", Assert.Single(history.Appended).Status);
    }

    [Fact]
    public async Task CancellationBetweenItemsReturnsAPartialResult()
    {
        using var folder = new TemporaryFolder();
        var first = folder.CreateFile("first.txt", "a");
        var second = folder.CreateFile("second.txt", "b");
        using var cancellation = new CancellationTokenSource();
        using var client = new FakeVirusTotalClient { CancelAfterUploadCount = 1 };
        client.Cancel = cancellation.Cancel;
        var history = new RecordingHistoryStore();
        var session = new UploadSession(client, history);

        var result = await session.RunAsync([Item(first), Item(second)], UploadMode.File, cancellation.Token);

        Assert.True(result.IsCanceled);
        Assert.Equal(1, result.UploadedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Single(result.ItemResults);
        Assert.Equal(1, client.CallCount);
        Assert.Single(history.Appended);
    }

    [Fact]
    public async Task AnAlreadyCanceledTokenThrowsBeforeAnyUpload()
    {
        using var folder = new TemporaryFolder();
        var file = folder.CreateFile("file.txt", "content");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var client = new FakeVirusTotalClient();
        var session = new UploadSession(client, new RecordingHistoryStore());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => session.RunAsync([Item(file)], UploadMode.File, cancellation.Token));

        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task AClientInternalTimeoutIsAFailureNotACancellation()
    {
        using var folder = new TemporaryFolder();
        var first = folder.CreateFile("first.txt", "a");
        var second = folder.CreateFile("second.txt", "b");
        var third = folder.CreateFile("third.txt", "c");
        using var client = new FakeVirusTotalClient { CancelSecondUploadWithoutCallerCancellation = true };
        var history = new RecordingHistoryStore();
        var session = new UploadSession(client, history);

        var result = await session.RunAsync(
            [Item(first), Item(second), Item(third)],
            UploadMode.File,
            CancellationToken.None);

        Assert.False(result.IsCanceled);
        Assert.Equal(2, result.UploadedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(
            "The upload timed out waiting for VirusTotal. Check your network connection and try again.",
            result.ItemResults[1].ErrorMessage);
        Assert.Equal(["uploaded", "failed", "uploaded"], history.Appended.Select(entry => entry.Status));
    }

    [Fact]
    public async Task AClientSuppliedHashIsNormalizedToLowercase()
    {
        using var folder = new TemporaryFolder();
        var file = folder.CreateFile("file.txt", "content");
        using var client = new FakeVirusTotalClient { FileSha256ToReturn = new string('A', 64) };
        var history = new RecordingHistoryStore();
        var session = new UploadSession(client, history);

        await session.RunAsync([Item(file)], UploadMode.File, CancellationToken.None);

        Assert.Equal(new string('a', 64), Assert.Single(history.Appended).Sha256);
    }

    [Fact]
    public async Task AMissingClientHashFallsBackToALocallyComputedHash()
    {
        using var folder = new TemporaryFolder();
        var file = folder.CreateFile("file.txt", "hash me locally");
        using var client = new FakeVirusTotalClient();
        var history = new RecordingHistoryStore();
        var session = new UploadSession(client, history);

        await session.RunAsync([Item(file)], UploadMode.File, CancellationToken.None);

        Assert.Equal(Sha256Of("hash me locally"), Assert.Single(history.Appended).Sha256);
    }

    [Fact]
    public async Task ExhaustedHistoryRetriesAreContainedBySession()
    {
        using var folder = new TemporaryFolder();
        var file = folder.CreateFile("file.txt", "content");
        var historyPath = Path.Combine(folder.Path, "history.jsonl");
        using var client = new FakeVirusTotalClient();
        var session = new UploadSession(client, new UploadHistoryStore(historyPath));

        UploadSessionResult result;
        using (new FileStream(historyPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            result = await session.RunAsync([Item(file)], UploadMode.File, CancellationToken.None);
        }

        var item = Assert.Single(result.ItemResults);
        Assert.Equal("uploaded", item.Status);
        Assert.NotNull(item.ResultUrl);
    }
}
