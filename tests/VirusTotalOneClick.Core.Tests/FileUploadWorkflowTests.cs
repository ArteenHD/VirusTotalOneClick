namespace VirusTotalOneClick.Core.Tests;

public class FileUploadWorkflowTests
{
    private static (FileUploadWorkflow Workflow, Func<bool> ClientFactoryInvoked) CreateWorkflow(
        FakeVirusTotalClient client,
        AppSettings? settings = null,
        string? apiKey = "test-key",
        IUploadHistoryStore? historyStore = null)
    {
        var invoked = false;
        var workflow = new FileUploadWorkflow(
            settings ?? AppSettings.Default,
            new FakeSecretStore(apiKey),
            _ =>
            {
                invoked = true;
                return client;
            },
            historyStore ?? new RecordingHistoryStore());
        return (workflow, () => invoked);
    }

    [Fact]
    public async Task AllBlankPathsReturnNoFiles()
    {
        using var client = new FakeVirusTotalClient();
        var (workflow, factoryInvoked) = CreateWorkflow(client);

        var result = await workflow.RunAsync(["", "   "], CancellationToken.None);

        Assert.Equal(FileUploadWorkflowStatus.NoFiles, result.Status);
        Assert.Equal(0, result.ItemCount);
        Assert.False(factoryInvoked());
    }

    [Fact]
    public async Task MissingFilesFailLocallyWithoutCallingTheClient()
    {
        using var folder = new TemporaryFolder();
        using var client = new FakeVirusTotalClient();
        var history = new RecordingHistoryStore();
        var (workflow, factoryInvoked) = CreateWorkflow(client, historyStore: history);
        var missing = Path.Combine(folder.Path, "vanished.txt");

        var result = await workflow.RunAsync([missing], CancellationToken.None);

        Assert.Equal(FileUploadWorkflowStatus.Failed, result.Status);
        Assert.Equal(1, result.ItemCount);
        Assert.Equal(1, result.FailedCount);
        var item = Assert.Single(result.ItemResults!);
        Assert.Equal("File no longer exists.", item.ErrorMessage);
        Assert.Equal("vanished.txt", item.SubmittedName);
        Assert.False(factoryInvoked());
        Assert.Empty(history.Appended);
    }

    [Fact]
    public async Task NoStoredApiKeyShortCircuitsBeforeClientConstruction()
    {
        using var folder = new TemporaryFolder();
        var file = folder.CreateFile("real.txt", "content");
        using var client = new FakeVirusTotalClient();
        var history = new RecordingHistoryStore();
        var (workflow, factoryInvoked) = CreateWorkflow(client, apiKey: null, historyStore: history);

        var result = await workflow.RunAsync([file], CancellationToken.None);

        Assert.Equal(FileUploadWorkflowStatus.MissingApiKey, result.Status);
        Assert.Equal(1, result.ItemCount);
        Assert.False(factoryInvoked());
        Assert.Empty(history.Appended);
    }

    [Fact]
    public async Task HappyPathScrubsTheSubmittedNameAndUploads()
    {
        using var folder = new TemporaryFolder();
        var file = folder.CreateFile("quarterly-report.txt", "sensitive content");
        using var client = new FakeVirusTotalClient();
        var history = new RecordingHistoryStore();
        var settings = AppSettings.Default with { ScrubSubmittedFilename = true };
        var (workflow, _) = CreateWorkflow(client, settings, historyStore: history);

        var result = await workflow.RunAsync([file], CancellationToken.None);

        Assert.Equal(FileUploadWorkflowStatus.Completed, result.Status);
        var upload = Assert.Single(client.Uploads);
        Assert.Matches("^sample-[0-9a-f]{8}\\.txt$", upload.SubmittedName);
        Assert.Equal("sensitive content", File.ReadAllText(file));
        Assert.Single(history.Appended);
    }

    [Fact]
    public async Task HistoryIsDisabledEntirelyWhenKeepLocalHistoryIsFalse()
    {
        using var folder = new TemporaryFolder();
        var file = folder.CreateFile("file.txt", "content");
        using var client = new FakeVirusTotalClient();
        var history = new RecordingHistoryStore();
        var settings = AppSettings.Default with { KeepLocalHistory = false };
        var (workflow, _) = CreateWorkflow(client, settings, historyStore: history);

        var result = await workflow.RunAsync([file], CancellationToken.None);

        Assert.Equal(FileUploadWorkflowStatus.Completed, result.Status);
        Assert.Empty(history.Appended);
    }

    [Fact]
    public async Task PartialFailuresReportCompletedWithFailures()
    {
        using var folder = new TemporaryFolder();
        var first = folder.CreateFile("first.txt", "a");
        var second = folder.CreateFile("second.txt", "b");
        using var client = new FakeVirusTotalClient { FailFirstUpload = true };
        var (workflow, _) = CreateWorkflow(client);

        var result = await workflow.RunAsync([first, second], CancellationToken.None);

        Assert.Equal(FileUploadWorkflowStatus.CompletedWithFailures, result.Status);
        Assert.Equal(2, result.ItemCount);
        Assert.Equal(1, result.FailedCount);
    }

    [Fact]
    public async Task AllUploadsFailingReportsFailed()
    {
        using var folder = new TemporaryFolder();
        var file = folder.CreateFile("file.txt", "content");
        using var client = new FakeVirusTotalClient { FailFirstUpload = true };
        var (workflow, _) = CreateWorkflow(client);

        var result = await workflow.RunAsync([file], CancellationToken.None);

        Assert.Equal(FileUploadWorkflowStatus.Failed, result.Status);
    }

    [Fact]
    public async Task MixedExistingAndMissingSelectionsReportBothInOrder()
    {
        using var folder = new TemporaryFolder();
        var existingFirst = folder.CreateFile("existing-first.txt", "a");
        var missing = Path.Combine(folder.Path, "missing.txt");
        var existingSecond = folder.CreateFile("existing-second.txt", "b");
        using var client = new FakeVirusTotalClient();
        var (workflow, _) = CreateWorkflow(client);

        var result = await workflow.RunAsync([existingFirst, missing, existingSecond], CancellationToken.None);

        Assert.Equal(FileUploadWorkflowStatus.CompletedWithFailures, result.Status);
        Assert.Equal(3, result.ItemCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(
            [existingFirst, existingSecond, missing],
            result.ItemResults!.Select(item => item.OriginalPath));
        Assert.Equal(["uploaded", "uploaded", "failed"], result.ItemResults!.Select(item => item.Status));
    }

    [Fact]
    public async Task AClientTimeoutReportsFailureNotCancellation()
    {
        using var folder = new TemporaryFolder();
        var first = folder.CreateFile("first.txt", "a");
        var second = folder.CreateFile("second.txt", "b");
        using var client = new FakeVirusTotalClient { CancelSecondUploadWithoutCallerCancellation = true };
        var (workflow, _) = CreateWorkflow(client);

        var result = await workflow.RunAsync([first, second], CancellationToken.None);

        Assert.Equal(FileUploadWorkflowStatus.CompletedWithFailures, result.Status);
        Assert.Contains(result.ItemResults!, item => item.ErrorMessage ==
            "The upload timed out waiting for VirusTotal. Check your network connection and try again.");
    }

    [Fact]
    public async Task ALockedFileDuringPreparationFailsOnlyThatItemWhenScrubbingIsOn()
    {
        using var folder = new TemporaryFolder();
        var first = folder.CreateFile("first.txt", "a");
        var locked = folder.CreateFile("locked.txt", "b");
        var third = folder.CreateFile("third.txt", "c");
        using var client = new FakeVirusTotalClient();
        var settings = AppSettings.Default with { ScrubSubmittedFilename = true };
        var (workflow, _) = CreateWorkflow(client, settings);

        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var result = await workflow.RunAsync([first, locked, third], CancellationToken.None);

            Assert.Equal(FileUploadWorkflowStatus.CompletedWithFailures, result.Status);
            Assert.Equal(3, result.ItemCount);
            Assert.Equal(1, result.FailedCount);
            Assert.Equal(2, client.Uploads.Count);
            var failedItem = Assert.Single(result.ItemResults!, item => item.Status == "failed");
            Assert.Equal("File could not be read.", failedItem.ErrorMessage);
        }
    }

    [Fact]
    public void FilePreparationRunsOffTheCallingThread()
    {
        using var folder = new TemporaryFolder();
        var file = folder.CreateFile("file.txt", "content");
        using var client = new FakeVirusTotalClient();
        var (workflow, _) = CreateWorkflow(client);
        var callingThreadId = 0;
        var workThreadId = 0;
        workflow.OnBackgroundWork = () => workThreadId = Environment.CurrentManagedThreadId;

        var thread = new Thread(() =>
        {
            callingThreadId = Environment.CurrentManagedThreadId;
            workflow.RunAsync([file], CancellationToken.None).GetAwaiter().GetResult();
        });
        thread.Start();
        thread.Join();

        Assert.NotEqual(0, workThreadId);
        Assert.NotEqual(callingThreadId, workThreadId);
    }
}
