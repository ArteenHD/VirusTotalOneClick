using System.Security.Cryptography;

namespace VirusTotalOneClick.Core;

public sealed class UploadSession
{
    private const string NetworkTimeoutErrorMessage =
        "The upload timed out waiting for VirusTotal. Check your network connection and try again.";

    private static readonly TimeSpan DefaultRateLimitRetryDelay = TimeSpan.FromSeconds(20);

    private readonly IVirusTotalClient _client;
    private readonly IUploadHistoryStore _historyStore;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;

    public UploadSession(
        IVirusTotalClient client,
        IUploadHistoryStore historyStore,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        _client = client;
        _historyStore = historyStore;
        _delayAsync = delayAsync ?? Task.Delay;
    }

    public async Task<UploadSessionResult> RunAsync(
        IReadOnlyList<AnonymizedUploadItem> items,
        UploadMode mode,
        CancellationToken cancellationToken)
    {
        var uploadedCount = 0;
        var failedCount = 0;
        var itemResults = new List<UploadSessionItemResult>();

        foreach (var item in items)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                if (itemResults.Count > 0)
                {
                    return new UploadSessionResult(items.Count, uploadedCount, failedCount, itemResults, IsCanceled: true);
                }

                cancellationToken.ThrowIfCancellationRequested();
            }

            try
            {
                var result = await UploadWithRateLimitRetryAsync(item, cancellationToken).ConfigureAwait(false);

                uploadedCount++;
                itemResults.Add(new UploadSessionItemResult(
                    item.OriginalPath,
                    item.SubmittedName,
                    "uploaded",
                    result.ResultUrl));
                TryAppendHistory(item, mode, "uploaded", result.ResultUrl, result.FileSha256);
            }
            catch (OperationCanceledException) when (itemResults.Count > 0 && cancellationToken.IsCancellationRequested)
            {
                return new UploadSessionResult(items.Count, uploadedCount, failedCount, itemResults, IsCanceled: true);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // The HTTP client canceled the request internally (network timeout); the
                // caller did not cancel, so report a clear failure instead of "canceled".
                failedCount++;
                itemResults.Add(new UploadSessionItemResult(
                    item.OriginalPath,
                    item.SubmittedName,
                    "failed",
                    ErrorMessage: NetworkTimeoutErrorMessage));
                TryAppendHistory(item, mode, "failed", resultUrl: null, fileSha256: null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                failedCount++;
                itemResults.Add(new UploadSessionItemResult(
                    item.OriginalPath,
                    item.SubmittedName,
                    "failed",
                    ErrorMessage: ex.Message));
                TryAppendHistory(item, mode, "failed", resultUrl: null, fileSha256: null);
            }
            finally
            {
                TryDeleteTemporaryUploadCopy(item);
            }
        }

        return new UploadSessionResult(items.Count, uploadedCount, failedCount, itemResults);
    }

    private async Task<VirusTotalUploadResult> UploadWithRateLimitRetryAsync(
        AnonymizedUploadItem item,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _client
                .UploadFileAsync(item.UploadPath, item.SubmittedName, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (VirusTotalApiException ex) when (ex.StatusCode == 429)
        {
            // Rate limited (the free tier allows 4 requests/minute). Wait and retry once.
            await _delayAsync(ex.RetryAfter ?? DefaultRateLimitRetryDelay, cancellationToken).ConfigureAwait(false);
            return await _client
                .UploadFileAsync(item.UploadPath, item.SubmittedName, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private void TryAppendHistory(
        AnonymizedUploadItem item,
        UploadMode mode,
        string status,
        string? resultUrl,
        string? fileSha256)
    {
        var sha256 = ResolveHistorySha256(item.UploadPath, fileSha256);
        if (sha256 is null)
        {
            return;
        }

        try
        {
            _historyStore.Append(new UploadHistoryEntry(
                OriginalPath: item.OriginalPath,
                SubmittedName: item.SubmittedName,
                Sha256: sha256,
                UploadedAtUtc: DateTimeOffset.UtcNow,
                Mode: mode.ToString().ToLowerInvariant(),
                Status: status,
                ResultUrl: resultUrl));
        }
        catch (Exception)
        {
            // Local history is best-effort and must never change the upload result.
        }
    }

    private static string? ResolveHistorySha256(string uploadPath, string? fileSha256)
    {
        if (fileSha256 is not null && IsValidSha256(fileSha256))
        {
            return fileSha256.ToLowerInvariant();
        }

        var localSha256 = TryComputeSha256(uploadPath);
        return IsValidSha256(localSha256) ? localSha256 : null;
    }

    private static void TryDeleteTemporaryUploadCopy(AnonymizedUploadItem item)
    {
        try
        {
            if (!string.Equals(item.OriginalPath, item.UploadPath, StringComparison.OrdinalIgnoreCase) &&
                File.Exists(item.UploadPath))
            {
                File.Delete(item.UploadPath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string? TryComputeSha256(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexStringLower(SHA256.HashData(stream));
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsValidSha256(string? sha256)
    {
        return sha256 is { Length: 64 } &&
            sha256.All(Uri.IsHexDigit);
    }
}
