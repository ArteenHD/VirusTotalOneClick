namespace VirusTotalOneClick.Core.Tests;

// One configurable fake drives every session/workflow edge case. Flags are
// independent; tests set exactly one failure mode at a time.
internal sealed class FakeVirusTotalClient : IVirusTotalClient, IDisposable
{
    private readonly List<FileStream> _heldStreams = [];
    private int _callCount;

    public int CallCount => _callCount;

    public List<(string FilePath, string SubmittedName)> Uploads { get; } = [];

    /// <summary>The first upload call throws <see cref="InvalidOperationException"/> with <see cref="FailureMessage"/>.</summary>
    public bool FailFirstUpload { get; set; }

    public string FailureMessage { get; set; } = "No quota left.";

    /// <summary>The first N upload calls throw a 429 <see cref="VirusTotalApiException"/> ("Rate limit exceeded.").</summary>
    public int RateLimitedCallCount { get; set; }

    /// <summary>Retry-After carried by the 429 exceptions; null omits it.</summary>
    public TimeSpan? RateLimitRetryAfter { get; set; }

    /// <summary>After this many successful uploads, <see cref="Cancel"/> is invoked (simulates the user canceling mid-session).</summary>
    public int? CancelAfterUploadCount { get; set; }

    public Action? Cancel { get; set; }

    /// <summary>The second upload call throws a bare <see cref="TaskCanceledException"/> — the HTTP client's own timeout, not caller cancellation.</summary>
    public bool CancelSecondUploadWithoutCallerCancellation { get; set; }

    /// <summary>Hash reported back by the API; null (default) matches the real client, which never populates it.</summary>
    public string? FileSha256ToReturn { get; set; }

    /// <summary>Deletes the uploaded file before returning — the file vanishes before any local hashing can happen.</summary>
    public bool DeleteUploadedFileBeforeReturning { get; set; }

    /// <summary>Opens the uploaded file with no sharing and holds it — cleanup and local hashing both hit a locked file.</summary>
    public bool LockUploadedFileBeforeReturning { get; set; }

    public Task<VirusTotalUploadResult> UploadFileAsync(
        string filePath,
        string submittedName,
        CancellationToken cancellationToken)
    {
        var call = ++_callCount;
        if (call <= RateLimitedCallCount)
        {
            throw new VirusTotalApiException("Rate limit exceeded.", statusCode: 429, RateLimitRetryAfter);
        }

        if (CancelSecondUploadWithoutCallerCancellation && call == 2)
        {
            throw new TaskCanceledException();
        }

        if (FailFirstUpload && call == 1)
        {
            throw new InvalidOperationException(FailureMessage);
        }

        Uploads.Add((filePath, submittedName));

        if (DeleteUploadedFileBeforeReturning)
        {
            File.Delete(filePath);
        }

        if (LockUploadedFileBeforeReturning)
        {
            _heldStreams.Add(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None));
        }

        if (CancelAfterUploadCount is { } cancelAfter && Uploads.Count >= cancelAfter)
        {
            Cancel?.Invoke();
        }

        return Task.FromResult(new VirusTotalUploadResult(
            $"analysis-{call}",
            FileSha256ToReturn,
            $"https://example.test/analysis/{call}"));
    }

    public Task<ApiKeyValidationStatus> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken)
        => Task.FromResult(ApiKeyValidationStatus.Valid);

    public void Dispose()
    {
        foreach (var stream in _heldStreams)
        {
            stream.Dispose();
        }

        _heldStreams.Clear();
    }
}
