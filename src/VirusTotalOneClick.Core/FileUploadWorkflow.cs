namespace VirusTotalOneClick.Core;

public sealed class FileUploadWorkflow
{
    public const string MissingFileErrorMessage = "File no longer exists.";

    private readonly AppSettings _settings;
    private readonly ISecretStore _secretStore;
    private readonly Func<string, IVirusTotalClient> _clientFactory;
    private readonly IUploadHistoryStore _historyStore;

    public FileUploadWorkflow(
        AppSettings settings,
        ISecretStore secretStore,
        Func<string, IVirusTotalClient> clientFactory,
        IUploadHistoryStore historyStore)
    {
        _settings = settings;
        _secretStore = secretStore;
        _clientFactory = clientFactory;
        _historyStore = historyStore;
    }

    // Test seam proving the preparation work runs off the calling thread.
    internal Action? OnBackgroundWork { get; set; }

    public async Task<FileUploadWorkflowResult> RunAsync(
        IReadOnlyList<string> filePaths,
        CancellationToken cancellationToken)
    {
        var candidateFiles = filePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToList();
        var files = candidateFiles
            .Where(File.Exists)
            .ToList();
        var missingFileResults = candidateFiles
            .Where(path => !File.Exists(path))
            .Select(CreateMissingFileResult)
            .ToList();

        if (candidateFiles.Count == 0)
        {
            return new FileUploadWorkflowResult(FileUploadWorkflowStatus.NoFiles, ItemCount: 0);
        }

        if (files.Count == 0)
        {
            return new FileUploadWorkflowResult(
                FileUploadWorkflowStatus.Failed,
                candidateFiles.Count,
                missingFileResults.Count,
                missingFileResults);
        }

        var apiKey = _secretStore.ReadApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new FileUploadWorkflowResult(FileUploadWorkflowStatus.MissingApiKey, candidateFiles.Count);
        }

        var anonymizerOptions = new AnonymizerOptions(
            ScrubSubmittedFilename: _settings.ScrubSubmittedFilename,
            ScrubZipInternalPaths: _settings.ScrubZipInternalPaths);
        // Preparing items hashes file contents; keep that work off the calling
        // (dispatcher) thread.
        var uploadItems = await Task.Run(
            () =>
            {
                OnBackgroundWork?.Invoke();
                return files
                    .Select(file => AnonymizerService.PrepareFile(file, anonymizerOptions))
                    .ToList();
            },
            cancellationToken).ConfigureAwait(false);

        var session = new UploadSession(
            _clientFactory(apiKey),
            _settings.KeepLocalHistory ? _historyStore : NullUploadHistoryStore.Instance);
        var sessionResult = await session.RunAsync(uploadItems, UploadMode.File, cancellationToken).ConfigureAwait(false);
        var itemResults = ToWorkflowItemResults(sessionResult.ItemResults)
            .Concat(missingFileResults)
            .ToList();
        var failedCount = sessionResult.FailedCount + missingFileResults.Count;

        return new FileUploadWorkflowResult(
            ToWorkflowStatus(sessionResult, failedCount),
            candidateFiles.Count,
            failedCount,
            itemResults);
    }

    private static FileUploadWorkflowStatus ToWorkflowStatus(UploadSessionResult result, int failedCount)
    {
        if (result.IsCanceled)
        {
            return FileUploadWorkflowStatus.Canceled;
        }

        if (failedCount == 0)
        {
            return FileUploadWorkflowStatus.Completed;
        }

        return result.UploadedCount == 0
            ? FileUploadWorkflowStatus.Failed
            : FileUploadWorkflowStatus.CompletedWithFailures;
    }

    private static List<FileUploadWorkflowItemResult> ToWorkflowItemResults(
        IReadOnlyList<UploadSessionItemResult> itemResults)
    {
        return itemResults
            .Select(item => new FileUploadWorkflowItemResult(
                item.OriginalPath,
                item.SubmittedName,
                item.Status,
                item.ResultUrl,
                item.ErrorMessage))
            .ToList();
    }

    private static FileUploadWorkflowItemResult CreateMissingFileResult(string originalPath)
    {
        var submittedName = Path.GetFileName(originalPath);
        if (string.IsNullOrWhiteSpace(submittedName))
        {
            submittedName = originalPath;
        }

        return new FileUploadWorkflowItemResult(
            originalPath,
            submittedName,
            "failed",
            ErrorMessage: MissingFileErrorMessage);
    }
}
