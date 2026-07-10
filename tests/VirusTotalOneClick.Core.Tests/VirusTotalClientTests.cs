using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace VirusTotalOneClick.Core.Tests;

public class VirusTotalClientTests
{
    private static readonly Uri BaseUri = new("https://www.virustotal.com/api/v3/");

    [Fact]
    public void DefaultUploadLimitsMatchTheDocumentedApiCeilings()
    {
        var options = new VirusTotalOptions("test-api-key", BaseUri);

        Assert.Equal(32L * 1024 * 1024, options.SmallFileUploadLimitBytes);
        Assert.Equal(650L * 1024 * 1024, options.MaxFileUploadLimitBytes);
    }

    [Fact]
    public void ApiKeyValidationStatusHasExactlyTheThreeExpectedValues()
    {
        Assert.Equal(
            new[] { "Valid", "Rejected", "NetworkError" },
            Enum.GetNames<ApiKeyValidationStatus>());
    }

    [Fact]
    public async Task SmallFileUploadsDirectlyToTheFilesEndpoint()
    {
        using var folder = new TemporaryFolder();
        var path = folder.CreateFile("QuarterlyNumbers.pdf", "small file content");
        var handler = new RecordingHttpHandler((_, _) => Json(HttpStatusCode.OK, """{"data":{"id":"abc123"}}"""));
        using var httpClient = new HttpClient(handler);
        var client = new VirusTotalClient(httpClient, new VirusTotalOptions("test-api-key", BaseUri));

        var result = await client.UploadFileAsync(path, "submitted.bin", CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://www.virustotal.com/api/v3/files", request.RequestUri!.ToString());
        Assert.Equal("test-api-key", Assert.Single(request.Headers.GetValues("x-apikey")));
        var body = handler.RequestBodies[0]!;
        Assert.Contains("name=file", body, StringComparison.Ordinal);
        Assert.Contains("submitted.bin", body, StringComparison.Ordinal);
        Assert.Contains("small file content", body, StringComparison.Ordinal);
        Assert.DoesNotContain("QuarterlyNumbers", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("abc123", result.AnalysisId);
        Assert.Null(result.FileSha256);
        Assert.Equal("https://www.virustotal.com/gui/file-analysis/abc123", result.ResultUrl);
    }

    [Fact]
    public async Task LargeFileFetchesAnUploadUrlThenPostsToIt()
    {
        using var folder = new TemporaryFolder();
        var path = folder.CreateFile("big.bin", "0123456789");
        var handler = new RecordingHttpHandler((_, index) => index == 0
            ? Json(HttpStatusCode.OK, """{"data":"https://big-uploads.virustotal.com/session-1"}""")
            : Json(HttpStatusCode.OK, """{"data":{"id":"large-analysis"}}"""));
        using var httpClient = new HttpClient(handler);
        var options = new VirusTotalOptions("test-api-key", BaseUri) with { SmallFileUploadLimitBytes = 4 };
        var client = new VirusTotalClient(httpClient, options);

        var result = await client.UploadFileAsync(path, "submitted.bin", CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal("https://www.virustotal.com/api/v3/files/upload_url", handler.Requests[0].RequestUri!.ToString());
        Assert.Equal("test-api-key", Assert.Single(handler.Requests[0].Headers.GetValues("x-apikey")));
        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
        Assert.Equal("https://big-uploads.virustotal.com/session-1", handler.Requests[1].RequestUri!.ToString());
        Assert.Equal("test-api-key", Assert.Single(handler.Requests[1].Headers.GetValues("x-apikey")));
        Assert.Contains("submitted.bin", handler.RequestBodies[1]!, StringComparison.Ordinal);
        Assert.Equal("large-analysis", result.AnalysisId);
    }

    [Fact]
    public async Task OversizedFileThrowsBeforeAnyHttpRequest()
    {
        using var folder = new TemporaryFolder();
        var path = folder.CreateFile("huge.bin", "12345");
        var handler = new RecordingHttpHandler((_, _) => Json(HttpStatusCode.OK, "{}"));
        using var httpClient = new HttpClient(handler);
        var options = new VirusTotalOptions("test-api-key", BaseUri) with { MaxFileUploadLimitBytes = 3 };
        var client = new VirusTotalClient(httpClient, options);

        var exception = await Assert.ThrowsAsync<VirusTotalApiException>(
            () => client.UploadFileAsync(path, "submitted.bin", CancellationToken.None));

        Assert.Equal(0, exception.StatusCode);
        Assert.Null(exception.RetryAfter);
        Assert.Contains("650 MB", exception.Message, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "VirusTotal rejected the API key.")]
    [InlineData(HttpStatusCode.Forbidden, "VirusTotal denied this request. Check API key permissions.")]
    [InlineData(HttpStatusCode.RequestEntityTooLarge, "VirusTotal rejected the file because it is too large.")]
    [InlineData(HttpStatusCode.TooManyRequests, "VirusTotal rate limit or quota reached. Wait and try again.")]
    [InlineData(HttpStatusCode.BadGateway, "VirusTotal request failed with HTTP 502.")]
    public async Task UploadErrorsMapToClearMessages(HttpStatusCode status, string expectedMessage)
    {
        using var folder = new TemporaryFolder();
        var path = folder.CreateFile("sample.bin", "content");
        var handler = new RecordingHttpHandler((_, _) => new HttpResponseMessage(status));
        using var httpClient = new HttpClient(handler);
        var client = new VirusTotalClient(httpClient, new VirusTotalOptions("test-api-key", BaseUri));

        var exception = await Assert.ThrowsAsync<VirusTotalApiException>(
            () => client.UploadFileAsync(path, "submitted.bin", CancellationToken.None));

        Assert.Equal((int)status, exception.StatusCode);
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public async Task JsonErrorBodyMessageIsExtractedWithoutLeakingRawJson()
    {
        using var folder = new TemporaryFolder();
        var path = folder.CreateFile("sample.bin", "content");
        var handler = new RecordingHttpHandler(
            (_, _) => Json(HttpStatusCode.BadRequest, """{"error":{"code":"BadRequestError","message":"Wrong file part name"}}"""));
        using var httpClient = new HttpClient(handler);
        var client = new VirusTotalClient(httpClient, new VirusTotalOptions("test-api-key", BaseUri));

        var exception = await Assert.ThrowsAsync<VirusTotalApiException>(
            () => client.UploadFileAsync(path, "submitted.bin", CancellationToken.None));

        Assert.Contains("Wrong file part name", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("BadRequestError", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("{", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonJsonErrorBodyFallsBackToTheGenericMessage()
    {
        using var folder = new TemporaryFolder();
        var path = folder.CreateFile("sample.bin", "content");
        var handler = new RecordingHttpHandler((_, _) => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("<html>Bad gateway</html>", Encoding.UTF8, "text/html"),
        });
        using var httpClient = new HttpClient(handler);
        var client = new VirusTotalClient(httpClient, new VirusTotalOptions("test-api-key", BaseUri));

        var exception = await Assert.ThrowsAsync<VirusTotalApiException>(
            () => client.UploadFileAsync(path, "submitted.bin", CancellationToken.None));

        Assert.Equal("VirusTotal request failed with HTTP 502.", exception.Message);
    }

    [Fact]
    public async Task RetryAfterSecondsHeaderIsParsed()
    {
        var exception = await UploadExpectingRateLimit(
            response => response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(42)));

        Assert.Equal(TimeSpan.FromSeconds(42), exception.RetryAfter);
    }

    [Fact]
    public async Task RetryAfterHttpDateHeaderYieldsAPositiveDelay()
    {
        var exception = await UploadExpectingRateLimit(
            response => response.Headers.RetryAfter = new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddSeconds(90)));

        Assert.NotNull(exception.RetryAfter);
        Assert.InRange(exception.RetryAfter!.Value.TotalSeconds, 1, 90);
    }

    [Fact]
    public async Task StaleRetryAfterDateYieldsNoRetryHint()
    {
        var exception = await UploadExpectingRateLimit(
            response => response.Headers.RetryAfter = new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddSeconds(-90)));

        Assert.Null(exception.RetryAfter);
    }

    [Fact]
    public async Task AnalysisIdCanArriveAsABareDataString()
    {
        using var folder = new TemporaryFolder();
        var path = folder.CreateFile("sample.bin", "content");
        var handler = new RecordingHttpHandler((_, _) => Json(HttpStatusCode.OK, """{"data":"raw-id"}"""));
        using var httpClient = new HttpClient(handler);
        var client = new VirusTotalClient(httpClient, new VirusTotalOptions("test-api-key", BaseUri));

        var result = await client.UploadFileAsync(path, "submitted.bin", CancellationToken.None);

        Assert.Equal("raw-id", result.AnalysisId);
        Assert.Equal("https://www.virustotal.com/gui/file-analysis/raw-id", result.ResultUrl);
    }

    [Fact]
    public async Task MissingAnalysisIdYieldsEmptyIdAndNoResultUrl()
    {
        using var folder = new TemporaryFolder();
        var path = folder.CreateFile("sample.bin", "content");
        var handler = new RecordingHttpHandler((_, _) => Json(HttpStatusCode.OK, """{"data":{"type":"analysis"}}"""));
        using var httpClient = new HttpClient(handler);
        var client = new VirusTotalClient(httpClient, new VirusTotalOptions("test-api-key", BaseUri));

        var result = await client.UploadFileAsync(path, "submitted.bin", CancellationToken.None);

        Assert.Equal(string.Empty, result.AnalysisId);
        Assert.Null(result.ResultUrl);
    }

    [Fact]
    public async Task AnalysisIdIsEscapedInTheResultUrl()
    {
        using var folder = new TemporaryFolder();
        var path = folder.CreateFile("sample.bin", "content");
        var handler = new RecordingHttpHandler((_, _) => Json(HttpStatusCode.OK, """{"data":{"id":"abc/12 3"}}"""));
        using var httpClient = new HttpClient(handler);
        var client = new VirusTotalClient(httpClient, new VirusTotalOptions("test-api-key", BaseUri));

        var result = await client.UploadFileAsync(path, "submitted.bin", CancellationToken.None);

        Assert.Equal("https://www.virustotal.com/gui/file-analysis/abc%2F12%203", result.ResultUrl);
    }

    [Fact]
    public async Task RateLimitedUploadUrlFetchSurfacesStatusAndRetryAfter()
    {
        using var folder = new TemporaryFolder();
        var path = folder.CreateFile("big.bin", "0123456789");
        var handler = new RecordingHttpHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(7));
            return response;
        });
        using var httpClient = new HttpClient(handler);
        var options = new VirusTotalOptions("test-api-key", BaseUri) with { SmallFileUploadLimitBytes = 4 };
        var client = new VirusTotalClient(httpClient, options);

        var exception = await Assert.ThrowsAsync<VirusTotalApiException>(
            () => client.UploadFileAsync(path, "submitted.bin", CancellationToken.None));

        Assert.Equal(429, exception.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(7), exception.RetryAfter);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task FailedUploadUrlFetchSurfacesTheHttpStatus()
    {
        using var folder = new TemporaryFolder();
        var path = folder.CreateFile("big.bin", "0123456789");
        var handler = new RecordingHttpHandler((_, _) => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var httpClient = new HttpClient(handler);
        var options = new VirusTotalOptions("test-api-key", BaseUri) with { SmallFileUploadLimitBytes = 4 };
        var client = new VirusTotalClient(httpClient, options);

        var exception = await Assert.ThrowsAsync<VirusTotalApiException>(
            () => client.UploadFileAsync(path, "submitted.bin", CancellationToken.None));

        Assert.Equal(500, exception.StatusCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task NetworkFailureDuringUploadPropagatesAsHttpRequestException()
    {
        using var folder = new TemporaryFolder();
        var path = folder.CreateFile("sample.bin", "content");
        var handler = new RecordingHttpHandler((_, _) => throw new HttpRequestException("connection reset"));
        using var httpClient = new HttpClient(handler);
        var client = new VirusTotalClient(httpClient, new VirusTotalOptions("test-api-key", BaseUri));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.UploadFileAsync(path, "submitted.bin", CancellationToken.None));
    }

    [Fact]
    public async Task ValidateApiKeyProbesUsersMeWithTheCandidateKey()
    {
        var handler = new RecordingHttpHandler((_, _) => Json(HttpStatusCode.OK, """{"data":{}}"""));
        using var httpClient = new HttpClient(handler);
        var client = new VirusTotalClient(httpClient, new VirusTotalOptions("stored-key", BaseUri));

        var status = await client.ValidateApiKeyAsync("candidate-key", CancellationToken.None);

        Assert.Equal(ApiKeyValidationStatus.Valid, status);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://www.virustotal.com/api/v3/users/me", request.RequestUri!.ToString());
        Assert.Equal("candidate-key", Assert.Single(request.Headers.GetValues("x-apikey")));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task RejectedStatusesMapToRejected(HttpStatusCode status)
    {
        var handler = new RecordingHttpHandler((_, _) => new HttpResponseMessage(status));
        using var httpClient = new HttpClient(handler);
        var client = new VirusTotalClient(httpClient, new VirusTotalOptions("stored-key", BaseUri));

        Assert.Equal(ApiKeyValidationStatus.Rejected, await client.ValidateApiKeyAsync("candidate-key", CancellationToken.None));
    }

    [Fact]
    public async Task OtherHttpStatusesMapToNetworkError()
    {
        var handler = new RecordingHttpHandler((_, _) => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var httpClient = new HttpClient(handler);
        var client = new VirusTotalClient(httpClient, new VirusTotalOptions("stored-key", BaseUri));

        Assert.Equal(ApiKeyValidationStatus.NetworkError, await client.ValidateApiKeyAsync("candidate-key", CancellationToken.None));
    }

    [Fact]
    public async Task NetworkFailureDuringValidationMapsToNetworkError()
    {
        var handler = new RecordingHttpHandler((_, _) => throw new HttpRequestException("no route to host"));
        using var httpClient = new HttpClient(handler);
        var client = new VirusTotalClient(httpClient, new VirusTotalOptions("stored-key", BaseUri));

        Assert.Equal(ApiKeyValidationStatus.NetworkError, await client.ValidateApiKeyAsync("candidate-key", CancellationToken.None));
    }

    private static async Task<VirusTotalApiException> UploadExpectingRateLimit(Action<HttpResponseMessage> configureResponse)
    {
        using var folder = new TemporaryFolder();
        var path = folder.CreateFile("sample.bin", "content");
        var handler = new RecordingHttpHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            configureResponse(response);
            return response;
        });
        using var httpClient = new HttpClient(handler);
        var client = new VirusTotalClient(httpClient, new VirusTotalOptions("test-api-key", BaseUri));

        return await Assert.ThrowsAsync<VirusTotalApiException>(
            () => client.UploadFileAsync(path, "submitted.bin", CancellationToken.None));
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
