using VirusTotalOneClick.Core;

namespace VirusTotalOneClick.UI;

public partial class FolderReviewWindow : AppWindow, IDisposable
{
    private readonly FolderReviewViewModel _viewModel;
    private readonly Func<CancellationToken, Task<FolderScanResult>> _scanAsync;
    private readonly CancellationTokenSource _scanCancellation = new();
    private bool _disposed;

    public FolderReviewWindow(
        string root,
        Func<CancellationToken, Task<FolderScanResult>> scanAsync,
        Func<IReadOnlyList<string>, Func<CancellationToken, Task<FileUploadWorkflowResult>>>? uploadActionFactory = null,
        Func<FolderZipUploadRequest, Func<CancellationToken, Task<FileUploadWorkflowResult>>>? zipUploadActionFactory = null,
        FolderUploadMode defaultFolderMode = FolderUploadMode.ReviewSelectedFiles,
        bool isDevMode = false,
        Action<
            IReadOnlyList<FileSelection>,
            Func<CancellationToken, Task<FileUploadWorkflowResult>>,
            bool,
            Func<IReadOnlyList<string>, Func<CancellationToken, Task<FileUploadWorkflowResult>>>?>? openUploadProgress = null,
        Action? progressHandoffCompletedAction = null,
        int scanFileLimit = 5000)
    {
        ArgumentNullException.ThrowIfNull(scanAsync);
        _scanAsync = scanAsync;
        IsDevMode = isDevMode;
        _viewModel = new FolderReviewViewModel(
            root,
            uploadActionFactory,
            zipUploadActionFactory,
            defaultFolderMode,
            openUploadProgress,
            progressHandoffCompletedAction,
            cancelAction: Close,
            scanFileLimit);
        DataContext = _viewModel;
        InitializeComponent();
        ScanCompletion = LoadAsync();
    }

    public bool IsDevMode { get; }

    public FolderReviewViewModel ViewModel => _viewModel;

    /// <summary>Completes when the scan has populated the review (or failed/canceled).</summary>
    public Task ScanCompletion { get; }

    public int CandidateFileCount => _viewModel.CandidateFileCount;

    public int SelectedFileCount => _viewModel.SelectedFileCount;

    public string WarningText => _viewModel.WarningText;

    public string ScanErrorText => _viewModel.ScanErrorText;

    public string ActionStatusText => _viewModel.ActionStatusText;

    public string DefaultActionText => _viewModel.DefaultActionText;

    public FolderReviewNode? RootNode => _viewModel.Root;

    public Task UploadSelectedFilesAsync(CancellationToken cancellationToken)
        => _viewModel.UploadSelectedFilesAsync(cancellationToken);

    public Task ZipSelectedItemsAsync(CancellationToken cancellationToken)
        => _viewModel.ZipSelectedItemsAsync(cancellationToken);

    protected override void OnClosed(EventArgs e)
    {
        _scanCancellation.Cancel();
        base.OnClosed(e);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _scanCancellation.Dispose();
        }

        _disposed = true;
    }

    private async Task LoadAsync()
    {
        FolderScanResult result;
        try
        {
            // The scan always leaves the calling thread so the window stays
            // responsive no matter how the scan function is implemented.
            result = await Task.Run(() => _scanAsync(_scanCancellation.Token)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            _viewModel.ApplyScanResult(new FolderScanResult([], Truncated: false, Errors: [ex.Message]));
            return;
        }

        _viewModel.ApplyScanResult(result);
    }
}
