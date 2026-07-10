using System.Net;
using System.Text.Json;

namespace VirusTotalOneClick.Core;

public sealed class VirusTotalClient : IVirusTotalClient
{
    private readonly HttpClient _httpClient;
    private readonly VirusTotalOptions _options;

    public VirusTotalClient(HttpClient httpClient, VirusTotalOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<VirusTotalUploadResult> UploadFileAsync(
        string filePath,
        string submittedName,
        CancellationToken cancellationToken)
    {
        var uploadUri = await GetUploadUriAsync(filePath, cancellationToken).ConfigureAwait(false);
        using var request = new HttpRequestMessage(HttpMethod.Post, uploadUri);
        AddApiKey(request);
        request.Content = CreateMultipartContent(filePath, submittedName);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var analysisId = ReadAnalysisId(json);
        return new VirusTotalUploadResult(
            analysisId,
            FileSha256: null,
            ResultUrl: string.IsNullOrWhiteSpace(analysisId)
                ? null
                : $"https://www.virustotal.com/gui/file-analysis/{Uri.EscapeDataString(analysisId)}");
    }

    public async Task<ApiKeyValidationStatus> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_options.BaseUri, "users/me"));
        request.Headers.Add("x-apikey", apiKey);
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return ApiKeyValidationStatus.Valid;
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return ApiKeyValidationStatus.Rejected;
            }

            return ApiKeyValidationStatus.NetworkError;
        }
        catch (HttpRequestException)
        {
            return ApiKeyValidationStatus.NetworkError;
        }
    }

    private async Task<Uri> GetUploadUriAsync(string filePath, CancellationToken cancellationToken)
    {
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > _options.MaxFileUploadLimitBytes)
        {
            throw new VirusTotalApiException("VirusTotal accepts file uploads up to 650 MB.", statusCode: 0);
        }

        if (fileInfo.Length <= _options.SmallFileUploadLimitBytes)
        {
            return new Uri(_options.BaseUri, "files");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_options.BaseUri, "files/upload_url"));
        AddApiKey(request);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var uploadUrl = ReadStringData(json);
        return new Uri(uploadUrl, UriKind.Absolute);
    }

    private static MultipartFormDataContent CreateMultipartContent(string filePath, string submittedName)
    {
        var content = new MultipartFormDataContent();
        var stream = File.OpenRead(filePath);
        content.Add(new StreamContent(stream), "file", submittedName);
        return content;
    }

    private void AddApiKey(HttpRequestMessage request)
    {
        request.Headers.Remove("x-apikey");
        request.Headers.Add("x-apikey", _options.ApiKey);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var statusCode = (int)response.StatusCode;
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "VirusTotal rejected the API key.",
            HttpStatusCode.Forbidden => "VirusTotal denied this request. Check API key permissions.",
            HttpStatusCode.RequestEntityTooLarge => "VirusTotal rejected the file because it is too large.",
            HttpStatusCode.TooManyRequests => "VirusTotal rate limit or quota reached. Wait and try again.",
            _ => $"VirusTotal request failed with HTTP {statusCode}.",
        };

        var errorDetail = TryReadErrorMessage(body);
        if (!string.IsNullOrWhiteSpace(errorDetail))
        {
            message += $" ({errorDetail})";
        }

        throw new VirusTotalApiException(message, statusCode, ReadRetryAfter(response));
    }

    private static string? TryReadErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.Object &&
                error.TryGetProperty("message", out var errorMessage) &&
                errorMessage.ValueKind == JsonValueKind.String)
            {
                return errorMessage.GetString();
            }
        }
        catch (JsonException)
        {
            // Not a JSON body; the status-specific message is enough on its own.
        }

        return null;
    }

    private static TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter is null)
        {
            return null;
        }

        if (retryAfter.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return delta;
        }

        if (retryAfter.Date is { } date)
        {
            var delay = date - DateTimeOffset.UtcNow;
            return delay > TimeSpan.Zero ? delay : null;
        }

        return null;
    }

    private static string ReadStringData(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.String)
        {
            return data.GetString() ?? string.Empty;
        }

        throw new VirusTotalApiException("VirusTotal response did not include an upload URL.", 200);
    }

    private static string ReadAnalysisId(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("data", out var data))
        {
            return string.Empty;
        }

        if (data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty("id", out var id) &&
            id.ValueKind == JsonValueKind.String)
        {
            return id.GetString() ?? string.Empty;
        }

        if (data.ValueKind == JsonValueKind.String)
        {
            return data.GetString() ?? string.Empty;
        }

        return string.Empty;
    }
}
