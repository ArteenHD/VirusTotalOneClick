using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace VirusTotalOneClick.Core.Tests;

public class UploadPipelineSmokeTests
{
    [Fact]
    public async Task AFileUploadFlowsThroughTheRealHttpClientAndHistoryStore()
    {
        using var folder = new TemporaryFolder();
        var file = folder.CreateFile("quarterly-report.txt", "hello smoke");
        var historyStore = new UploadHistoryStore(Path.Combine(folder.Path, "history.jsonl"));
        var handler = new RecordingHttpHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"data\":{\"id\":\"abc123\",\"type\":\"analysis\"}}"),
        });
        using var httpClient = new HttpClient(handler);
        var settings = AppSettings.Default with { ScrubSubmittedFilename = true };
        var workflow = new FileUploadWorkflow(
            settings,
            new FakeSecretStore("smoke-api-key"),
            apiKey => new VirusTotalClient(
                httpClient,
                new VirusTotalOptions(apiKey, new Uri("https://www.virustotal.com/api/v3/"))),
            historyStore);

        var result = await workflow.RunAsync([file], CancellationToken.None);

        Assert.Equal(FileUploadWorkflowStatus.Completed, result.Status);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/api/v3/files", request.RequestUri!.AbsolutePath);
        Assert.Equal("smoke-api-key", Assert.Single(request.Headers.GetValues("x-apikey")));
        var body = Assert.Single(handler.RequestBodies);
        Assert.Contains("hello smoke", body, StringComparison.Ordinal);
        Assert.Contains("filename=sample-", body, StringComparison.Ordinal);
        Assert.DoesNotContain("quarterly-report", body, StringComparison.OrdinalIgnoreCase);
        var entry = Assert.Single(historyStore.ReadAll());
        Assert.Equal("uploaded", entry.Status);
        Assert.Equal("https://www.virustotal.com/gui/file-analysis/abc123", entry.ResultUrl);
        Assert.StartsWith("sample-", entry.SubmittedName, StringComparison.Ordinal);
        Assert.Equal(file, entry.OriginalPath);
        Assert.Equal("file", entry.Mode);
        Assert.Equal(
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("hello smoke"))),
            entry.Sha256);
    }
}
