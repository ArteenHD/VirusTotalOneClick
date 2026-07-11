namespace VirusTotalOneClick.Core.Tests;

public class FileUploadActionFactoryTests
{
    [Fact]
    public async Task CreateBuildsARunnableUploadAction()
    {
        using var folder = new TemporaryFolder();
        var file = folder.CreateFile("file.txt", "content");
        using var client = new FakeVirusTotalClient();
        var factory = new FileUploadActionFactory(
            AppSettings.Default,
            new FakeSecretStore("test-key"),
            _ => client,
            new RecordingHistoryStore());

        var action = factory.Create([file]);
        var result = await action(CancellationToken.None);

        Assert.Equal(FileUploadWorkflowStatus.Completed, result.Status);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task EachInvocationRunsIndependently()
    {
        using var folder = new TemporaryFolder();
        var file = folder.CreateFile("file.txt", "content");
        using var client = new FakeVirusTotalClient();
        var factory = new FileUploadActionFactory(
            AppSettings.Default,
            new FakeSecretStore("test-key"),
            _ => client,
            new RecordingHistoryStore());
        var action = factory.Create([file]);

        var first = await action(CancellationToken.None);
        var second = await action(CancellationToken.None);

        Assert.Equal(FileUploadWorkflowStatus.Completed, first.Status);
        Assert.Equal(FileUploadWorkflowStatus.Completed, second.Status);
        Assert.Equal(2, client.CallCount);
    }
}
