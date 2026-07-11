namespace VirusTotalOneClick.Core.Tests;

public class FolderZipUploadWorkflowTests
{
    private const string OversizeErrorMessage =
        "The selected files total more than 650 MB, which exceeds the VirusTotal upload limit. Deselect some files and try again.";

    private static FolderZipUploadWorkflow CreateWorkflow(
        FakeVirusTotalClient client,
        string temporaryDirectory,
        AppSettings? settings = null,
        string? apiKey = "test-key",
        IUploadHistoryStore? historyStore = null,
        long? maxTotalUploadSizeBytes = null)
    {
        return new FolderZipUploadWorkflow(
            settings ?? AppSettings.Default,
            new FakeSecretStore(apiKey),
            _ => client,
            historyStore ?? new RecordingHistoryStore(),
            temporaryDirectory,
            maxTotalUploadSizeBytes ?? VirusTotalOptions.DefaultMaxFileUploadLimitBytes);
    }

    [Fact]
    public async Task SelectedFilesZipUploadAndTheTempZipIsRemoved()
    {
        using var folder = new TemporaryFolder();
        var source = folder.CreateSubdirectory("source");
        var first = folder.CreateFile(@"source\first.txt", "alpha");
        var second = folder.CreateFile(@"source\second.txt", "beta");
        var scratch = Path.Combine(folder.Path, "scratch");
        using var client = new FakeVirusTotalClient();
        var history = new RecordingHistoryStore();
        var workflow = CreateWorkflow(client, scratch, historyStore: history);

        var result = await workflow.RunAsync([first, second], CancellationToken.None);

        Assert.Equal(FileUploadWorkflowStatus.Completed, result.Status);
        Assert.Equal(1, result.ItemCount);
        var upload = Assert.Single(client.Uploads);
        Assert.EndsWith(".zip", upload.SubmittedName, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFileSystemEntries(scratch));
        var entry = Assert.Single(history.Appended);
        Assert.Equal("zip", entry.Mode);
        Assert.Equal(source, Assert.Single(result.ItemResults!).OriginalPath);
    }

    [Fact]
    public async Task ATempZipThatCannotBeDeletedStillReportsCompleted()
    {
        using var folder = new TemporaryFolder();
        var file = folder.CreateFile("file.txt", "content");
        var scratch = Path.Combine(folder.Path, "scratch");
        using var client = new FakeVirusTotalClient { LockUploadedFileBeforeReturning = true };
        var workflow = CreateWorkflow(client, scratch);

        var result = await workflow.RunAsync([file], CancellationToken.None);

        Assert.Equal(FileUploadWorkflowStatus.Completed, result.Status);
        Assert.Single(Directory.EnumerateFiles(scratch, "*.zip"));
    }

    [Fact]
    public async Task TheRequestOverloadMapsResultsToTheSourceRoot()
    {
        using var folder = new TemporaryFolder();
        var source = folder.CreateSubdirectory("chosen-folder");
        var file = folder.CreateFile(@"chosen-folder\inner.txt", "content");
        var scratch = Path.Combine(folder.Path, "scratch");
        using var client = new FakeVirusTotalClient();
        var workflow = CreateWorkflow(client, scratch);

        var result = await workflow.RunAsync(
            new FolderZipUploadRequest(source, [file]),
            CancellationToken.None);

        Assert.Equal(source, Assert.Single(result.ItemResults!).OriginalPath);
    }

    [Fact]
    public async Task TheHistoryEntryPointsAtTheSourceFolderNotTheTempZip()
    {
        using var folder = new TemporaryFolder();
        var source = folder.CreateSubdirectory("chosen-folder");
        var file = folder.CreateFile(@"chosen-folder\inner.txt", "content");
        var scratch = Path.Combine(folder.Path, "scratch");
        using var client = new FakeVirusTotalClient();
        var history = new RecordingHistoryStore();
        var workflow = CreateWorkflow(client, scratch, historyStore: history);

        await workflow.RunAsync(new FolderZipUploadRequest(source, [file]), CancellationToken.None);

        var entry = Assert.Single(history.Appended);
        Assert.Equal(source, entry.OriginalPath);
        Assert.DoesNotContain("virustotal-oneclick-", entry.OriginalPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OneMissingFileFailsTheEntireSelection()
    {
        using var folder = new TemporaryFolder();
        var source = folder.CreateSubdirectory("source");
        var existing = folder.CreateFile(@"source\existing.txt", "content");
        var missing = Path.Combine(source, "missing.txt");
        var scratch = Path.Combine(folder.Path, "scratch");
        using var client = new FakeVirusTotalClient();
        var history = new RecordingHistoryStore();
        var workflow = CreateWorkflow(client, scratch, historyStore: history);

        var result = await workflow.RunAsync([existing, missing], CancellationToken.None);

        Assert.Equal(FileUploadWorkflowStatus.Failed, result.Status);
        Assert.Equal(1, result.ItemCount);
        var item = Assert.Single(result.ItemResults!);
        Assert.Equal("One or more selected files no longer exists.", item.ErrorMessage);
        Assert.Equal("source", item.SubmittedName);
        Assert.Equal(0, client.CallCount);
        Assert.Empty(history.Appended);
        Assert.False(Directory.Exists(scratch) && Directory.EnumerateFileSystemEntries(scratch).Any());
    }

    [Fact]
    public async Task AnEntirelyMissingSelectionFailsTheSameWay()
    {
        using var folder = new TemporaryFolder();
        var source = folder.CreateSubdirectory("source");
        var missing = Path.Combine(source, "missing.txt");
        var scratch = Path.Combine(folder.Path, "scratch");
        using var client = new FakeVirusTotalClient();
        var workflow = CreateWorkflow(client, scratch);

        var result = await workflow.RunAsync([missing], CancellationToken.None);

        Assert.Equal(FileUploadWorkflowStatus.Failed, result.Status);
        Assert.Equal("One or more selected files no longer exists.",
            Assert.Single(result.ItemResults!).ErrorMessage);
        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task AnOversizedSelectionFailsFastWithoutBuildingAZip()
    {
        using var folder = new TemporaryFolder();
        var source = folder.CreateSubdirectory("source");
        var first = folder.CreateFile(@"source\first.bin", "12345678");
        var second = folder.CreateFile(@"source\second.bin", "12345678");
        var scratch = Path.Combine(folder.Path, "scratch");
        using var client = new FakeVirusTotalClient();
        var workflow = CreateWorkflow(client, scratch, maxTotalUploadSizeBytes: 10);

        var result = await workflow.RunAsync([first, second], CancellationToken.None);

        Assert.Equal(FileUploadWorkflowStatus.Failed, result.Status);
        Assert.Equal(OversizeErrorMessage, Assert.Single(result.ItemResults!).ErrorMessage);
        Assert.Equal(0, client.CallCount);
        Assert.False(Directory.Exists(scratch) && Directory.EnumerateFileSystemEntries(scratch).Any());
    }

    [Fact]
    public async Task BlankSelectionsReturnNoFiles()
    {
        using var folder = new TemporaryFolder();
        var scratch = Path.Combine(folder.Path, "scratch");
        using var client = new FakeVirusTotalClient();
        var workflow = CreateWorkflow(client, scratch);

        var result = await workflow.RunAsync(["", "  "], CancellationToken.None);

        Assert.Equal(FileUploadWorkflowStatus.NoFiles, result.Status);
        Assert.Equal(0, result.ItemCount);
    }

    [Fact]
    public async Task MissingApiKeyShortCircuits()
    {
        using var folder = new TemporaryFolder();
        var first = folder.CreateFile("first.txt", "a");
        var second = folder.CreateFile("second.txt", "b");
        var scratch = Path.Combine(folder.Path, "scratch");
        using var client = new FakeVirusTotalClient();
        var workflow = CreateWorkflow(client, scratch, apiKey: null);

        var result = await workflow.RunAsync([first, second], CancellationToken.None);

        Assert.Equal(FileUploadWorkflowStatus.MissingApiKey, result.Status);
        Assert.Equal(2, result.ItemCount);
        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public void ZipBuildingRunsOffTheCallingThread()
    {
        using var folder = new TemporaryFolder();
        var file = folder.CreateFile("file.txt", "content");
        var scratch = Path.Combine(folder.Path, "scratch");
        using var client = new FakeVirusTotalClient();
        var workflow = CreateWorkflow(client, scratch);
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
