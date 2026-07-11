namespace VirusTotalOneClick.Core;

public sealed class FolderZipUploadWorkflow
{
    private const string MissingSelectedFileErrorMessage = "One or more selected files no longer exists.";
    private const string OversizedSelectionErrorMessage =
        "The selected files total more than 650 MB, which exceeds the VirusTotal upload limit. Deselect some files and try again.";

    private readonly AppSettings _settings;
    private readonly ISecretStore _secretStore;
    private readonly Func<string, IVirusTotalClient> _clientFactory;
    private readonly IUploadHistoryStore _historyStore;
    private readonly string _temporaryDirectory;
    private readonly long _maxTotalUploadSizeBytes;

    public FolderZipUploadWorkflow(
        AppSettings settings,
        ISecretStore secretStore,
        Func<string, IVirusTotalClient> clientFactory,
        IUploadHistoryStore historyStore,
        string? temporaryDirectory = null,
        long maxTotalUploadSizeBytes = VirusTotalOptions.DefaultMaxFileUploadLimitBytes)
    {
        _settings = settings;
        _secretStore = secretStore;
        _clientFactory = clientFactory;
        _historyStore = historyStore;
        _temporaryDirectory = temporaryDirectory ?? Path.GetTempPath();
        _maxTotalUploadSizeBytes = maxTotalUploadSizeBytes;
    }

    // Test seam proving the zip build runs off the calling thread.
    internal Action? OnBackgroundWork { get; set; }

    public Task<FileUploadWorkflowResult> RunAsync(
        FolderZipUploadRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return RunAsync(request.SelectedFiles, request.SourceRootPath, cancellationToken);
    }

    public async Task<FileUploadWorkflowResult> RunAsync(
        IReadOnlyList<string> selectedFiles,
        CancellationToken cancellationToken) =>
        await RunAsync(selectedFiles, FindSourceRoot(selectedFiles), cancellationToken).ConfigureAwait(false);

    private async Task<FileUploadWorkflowResult> RunAsync(
        IReadOnlyList<string> selectedFiles,
        string? sourceRootPath,
        CancellationToken cancellationToken)
    {
        var files = selectedFiles
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToList();
        if (files.Count == 0)
        {
            return new FileUploadWorkflowResult(FileUploadWorkflowStatus.NoFiles, ItemCount: 0);
        }

        if (files.Any(path => !File.Exists(path)))
        {
            return CreateFailedSelectionResult(
                sourceRootPath ?? FindSourceRoot(files),
                MissingSelectedFileErrorMessage);
        }

        var apiKey = _secretStore.ReadApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new FileUploadWorkflowResult(FileUploadWorkflowStatus.MissingApiKey, files.Count);
        }

        var totalSizeBytes = files.Sum(file => new FileInfo(file).Length);
        if (totalSizeBytes > _maxTotalUploadSizeBytes)
        {
            return CreateFailedSelectionResult(
                sourceRootPath ?? FindSourceRoot(files),
                OversizedSelectionErrorMessage);
        }

        Directory.CreateDirectory(_temporaryDirectory);
        var zipPath = Path.Combine(_temporaryDirectory, $"virustotal-oneclick-{Guid.NewGuid():N}.zip");
        try
        {
            // Zip compression and hashing are CPU/IO heavy; keep them off the calling
            // (dispatcher) thread.
            var preparedItem = await Task.Run(
                () =>
                {
                    OnBackgroundWork?.Invoke();
                    ZipArchiveBuilder.Build(new ZipPlan(files, zipPath, _settings.ScrubZipInternalPaths), cancellationToken);
                    return AnonymizerService.PrepareFile(
                        zipPath,
                        new AnonymizerOptions(_settings.ScrubSubmittedFilename, _settings.ScrubZipInternalPaths));
                },
                cancellationToken).ConfigureAwait(false);
            var item = string.IsNullOrWhiteSpace(sourceRootPath)
                ? preparedItem
                : preparedItem with { OriginalPath = sourceRootPath };
            var session = new UploadSession(
                _clientFactory(apiKey),
                _settings.KeepLocalHistory ? _historyStore : NullUploadHistoryStore.Instance);

            var sessionResult = await session.RunAsync([item], UploadMode.Zip, cancellationToken).ConfigureAwait(false);
            return new FileUploadWorkflowResult(
                sessionResult.IsCanceled
                    ? FileUploadWorkflowStatus.Canceled
                    : sessionResult.FailedCount == 0
                        ? FileUploadWorkflowStatus.Completed
                        : FileUploadWorkflowStatus.Failed,
                sessionResult.ItemCount,
                sessionResult.FailedCount,
                sessionResult.ItemResults
                    .Select(result => new FileUploadWorkflowItemResult(
                        string.IsNullOrWhiteSpace(sourceRootPath) ? result.OriginalPath : sourceRootPath,
                        result.SubmittedName,
                        result.Status,
                        result.ResultUrl,
                        result.ErrorMessage))
                    .ToList());
        }
        finally
        {
            TryDeleteTemporaryZip(zipPath);
        }
    }

    private static void TryDeleteTemporaryZip(string zipPath)
    {
        try
        {
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string FindSourceRoot(IReadOnlyList<string> selectedFiles)
    {
        var first = selectedFiles.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
        if (first is null)
        {
            return string.Empty;
        }

        var fullPath = Path.GetFullPath(first);
        return Path.GetDirectoryName(fullPath) ?? fullPath;
    }

    private static FileUploadWorkflowResult CreateFailedSelectionResult(string sourceRootPath, string errorMessage)
    {
        var submittedName = Path.GetFileName(sourceRootPath);
        if (string.IsNullOrWhiteSpace(submittedName))
        {
            submittedName = sourceRootPath;
        }

        return new FileUploadWorkflowResult(
            FileUploadWorkflowStatus.Failed,
            ItemCount: 1,
            FailedCount: 1,
            ItemResults:
            [
                new FileUploadWorkflowItemResult(
                    sourceRootPath,
                    submittedName,
                    "failed",
                    ErrorMessage: errorMessage)
            ]);
    }
}
