using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using VirusTotalOneClick.Core;

namespace VirusTotalOneClick.UI;

public sealed class FolderReviewViewModel : ObservableObject
{
    public const string InfoCalloutText =
        "Uploading individual files gives clearer VirusTotal results. Uploading a zip preserves the bundle but may reduce result clarity.";

    private readonly Func<IReadOnlyList<string>, Func<CancellationToken, Task<FileUploadWorkflowResult>>>? _uploadActionFactory;
    private readonly Func<FolderZipUploadRequest, Func<CancellationToken, Task<FileUploadWorkflowResult>>>? _zipUploadActionFactory;
    private readonly Action<
        IReadOnlyList<FileSelection>,
        Func<CancellationToken, Task<FileUploadWorkflowResult>>,
        bool,
        Func<IReadOnlyList<string>, Func<CancellationToken, Task<FileUploadWorkflowResult>>>?>? _openUploadProgress;
    private readonly Action? _progressHandoffCompletedAction;
    private readonly int _scanFileLimit;

    private bool _isLoading = true;
    private string _actionStatusText = "Waiting for review action.";
    private string _scanErrorText = string.Empty;
    private int _candidateFileCount;
    private bool _truncated;
    private FolderReviewNode? _root;
    private IReadOnlyList<FolderReviewNode> _rootNodes = [];

    public FolderReviewViewModel(
        string rootPath,
        Func<IReadOnlyList<string>, Func<CancellationToken, Task<FileUploadWorkflowResult>>>? uploadActionFactory = null,
        Func<FolderZipUploadRequest, Func<CancellationToken, Task<FileUploadWorkflowResult>>>? zipUploadActionFactory = null,
        FolderUploadMode defaultFolderMode = FolderUploadMode.ReviewSelectedFiles,
        Action<
            IReadOnlyList<FileSelection>,
            Func<CancellationToken, Task<FileUploadWorkflowResult>>,
            bool,
            Func<IReadOnlyList<string>, Func<CancellationToken, Task<FileUploadWorkflowResult>>>?>? openUploadProgress = null,
        Action? progressHandoffCompletedAction = null,
        Action? cancelAction = null,
        int scanFileLimit = 5000)
    {
        RootPath = rootPath ?? string.Empty;
        _uploadActionFactory = uploadActionFactory;
        _zipUploadActionFactory = zipUploadActionFactory;
        _openUploadProgress = openUploadProgress;
        _progressHandoffCompletedAction = progressHandoffCompletedAction;
        _scanFileLimit = scanFileLimit;
        DefaultFolderMode = defaultFolderMode;

        UploadSelectedFilesCommand = new AsyncRelayCommand(
            () => UploadSelectedFilesAsync(CancellationToken.None),
            () => !IsLoading && _uploadActionFactory is not null);
        ZipSelectedItemsCommand = new AsyncRelayCommand(
            () => ZipSelectedItemsAsync(CancellationToken.None),
            () => !IsLoading && _zipUploadActionFactory is not null);
        CancelCommand = new RelayCommand(() => cancelAction?.Invoke());
    }

    public string RootPath { get; }

    public FolderUploadMode DefaultFolderMode { get; }

    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Bound via {Binding LoadingStatusText}; WPF only resolves instance properties.")]
    public string LoadingStatusText => "Scanning folder contents...";

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetValue(ref _isLoading, value))
            {
                RaiseActionAvailabilityChanged();
            }
        }
    }

    public int CandidateFileCount
    {
        get => _candidateFileCount;
        private set => SetValue(ref _candidateFileCount, value);
    }

    public bool Truncated
    {
        get => _truncated;
        private set => SetValue(ref _truncated, value);
    }

    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Bound via {Binding WarningText}; WPF only resolves instance properties.")]
    public string WarningText => InfoCalloutText;

    public bool HasTruncationWarning => Truncated;

    public string TruncationWarningText => Truncated
        ? FormattableString.Invariant($"Only the first {_scanFileLimit:N0} files are shown. Narrow the folder or upload a smaller selection for a complete review.")
        : string.Empty;

    public int SelectedFileCount => _root?.SelectedFiles().Count() ?? 0;

    public string SelectionSummary
    {
        get
        {
            if (_root is null)
            {
                return string.Empty;
            }

            var selected = _root.SelectedFiles().ToList();
            var totalBytes = selected.Sum(file => file.SizeBytes ?? 0);
            var word = selected.Count == 1 ? "file" : "files";
            return $"{selected.Count} {word} selected · Total size: {SizeFormat.Format(totalBytes)}";
        }
    }

    public string DefaultActionText => DefaultFolderMode == FolderUploadMode.ReviewThenZip
        ? "Zip selected items and upload"
        : "Upload selected files";

    public bool IsUploadPrimary => DefaultFolderMode != FolderUploadMode.ReviewThenZip;

    public bool IsZipPrimary => DefaultFolderMode == FolderUploadMode.ReviewThenZip;

    public string ScanErrorText
    {
        get => _scanErrorText;
        private set
        {
            if (SetValue(ref _scanErrorText, value))
            {
                RaisePropertyChanged(nameof(HasScanErrors));
            }
        }
    }

    public bool HasScanErrors => _scanErrorText.Length > 0;

    public string ActionStatusText
    {
        get => _actionStatusText;
        private set => SetValue(ref _actionStatusText, value);
    }

    public FolderReviewNode? Root
    {
        get => _root;
        private set => SetValue(ref _root, value);
    }

    public IReadOnlyList<FolderReviewNode> RootNodes
    {
        get => _rootNodes;
        private set => SetValue(ref _rootNodes, value);
    }

    public AsyncRelayCommand UploadSelectedFilesCommand { get; }

    public AsyncRelayCommand ZipSelectedItemsCommand { get; }

    public RelayCommand CancelCommand { get; }

    /// <summary>
    /// Populates the review from a completed scan. Only scalar properties are
    /// assigned (the node list is swapped whole), so this is safe to call from
    /// the scan's background continuation.
    /// </summary>
    public void ApplyScanResult(FolderScanResult scanResult)
    {
        ArgumentNullException.ThrowIfNull(scanResult);

        CandidateFileCount = scanResult.Files.Count;
        Truncated = scanResult.Truncated;
        RaisePropertyChanged(nameof(HasTruncationWarning));
        RaisePropertyChanged(nameof(TruncationWarningText));

        if (scanResult.Errors.Count > 0)
        {
            var word = scanResult.Errors.Count == 1 ? "item" : "items";
            ScanErrorText =
                $"{scanResult.Errors.Count} {word} could not be scanned: {string.Join("; ", scanResult.Errors)}";
        }

        var root = FolderReviewTreeBuilder.Build(RootPath, scanResult.Files);
        SubscribeToSelectionChanges(root);
        Root = root;
        RootNodes = root is null ? [] : [root];
        IsLoading = false;
        NotifySelectionChanged();
    }

    public IReadOnlyList<string> GetSelectedFilePaths()
        => _root is null ? [] : _root.SelectedFiles().Select(node => node.FullPath).ToList();

    public Task UploadSelectedFilesAsync(CancellationToken cancellationToken)
    {
        if (_uploadActionFactory is null)
        {
            ActionStatusText = "No upload action configured.";
            return Task.CompletedTask;
        }

        var paths = GetSelectedFilePaths();
        if (paths.Count == 0)
        {
            ActionStatusText = "No files selected.";
            return Task.CompletedTask;
        }

        var action = _uploadActionFactory(paths);
        if (_openUploadProgress is not null)
        {
            var selections = paths.Select(path => new FileSelection(path, IsDirectory: false)).ToList();
            _openUploadProgress(selections, action, true, _uploadActionFactory);
            ActionStatusText = "Upload opened in progress window.";
            _progressHandoffCompletedAction?.Invoke();
            return Task.CompletedTask;
        }

        return RunUploadAsync(action, cancellationToken);
    }

    public Task ZipSelectedItemsAsync(CancellationToken cancellationToken)
    {
        if (_zipUploadActionFactory is null)
        {
            ActionStatusText = "No upload action configured.";
            return Task.CompletedTask;
        }

        var paths = GetSelectedFilePaths();
        if (paths.Count == 0)
        {
            ActionStatusText = "No files selected.";
            return Task.CompletedTask;
        }

        var action = _zipUploadActionFactory(new FolderZipUploadRequest(RootPath, paths));
        if (_openUploadProgress is not null)
        {
            // The zip bundles every selected file into one upload; there is no
            // per-path subset to retry, so no retry factory is offered here.
            _openUploadProgress([new FileSelection(RootPath, IsDirectory: true)], action, true, null);
            ActionStatusText = "Zip upload opened in progress window.";
            _progressHandoffCompletedAction?.Invoke();
            return Task.CompletedTask;
        }

        return RunUploadAsync(action, cancellationToken);
    }

    private async Task RunUploadAsync(
        Func<CancellationToken, Task<FileUploadWorkflowResult>> action,
        CancellationToken cancellationToken)
    {
        ActionStatusText = "Uploading...";
        try
        {
            var result = await action(cancellationToken).ConfigureAwait(true);
            ActionStatusText = FormatStatus(result);
        }
        catch (Exception ex)
        {
            ActionStatusText = $"Upload failed: {ex.Message}";
        }
    }

    private static string FormatStatus(FileUploadWorkflowResult result)
    {
        return result.Status switch
        {
            FileUploadWorkflowStatus.Completed => $"Uploaded {result.ItemCount} item(s).",
            FileUploadWorkflowStatus.CompletedWithFailures =>
                $"Uploaded {result.ItemCount - result.FailedCount} item(s), {result.FailedCount} failed.",
            FileUploadWorkflowStatus.Canceled => FormatCanceledStatus(result),
            FileUploadWorkflowStatus.Failed => $"Upload failed for {result.FailedCount} item(s).",
            FileUploadWorkflowStatus.MissingApiKey => "API key required before upload.",
            FileUploadWorkflowStatus.NoFiles => "No files to upload.",
            _ => "Upload finished.",
        };
    }

    private static string FormatCanceledStatus(FileUploadWorkflowResult result)
    {
        var uploadedCount = result.ItemResults?.Count(item =>
            string.Equals(item.Status, "uploaded", StringComparison.OrdinalIgnoreCase)) ?? 0;
        return $"Upload canceled after {uploadedCount} of {result.ItemCount} item(s) uploaded.";
    }

    private void SubscribeToSelectionChanges(FolderReviewNode? node)
    {
        if (node is null)
        {
            return;
        }

        node.PropertyChanged += OnNodePropertyChanged;
        node.CheckedCascadeCompleted += OnNodeCascadeCompleted;
        foreach (var child in node.Children)
        {
            SubscribeToSelectionChanges(child);
        }
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FolderReviewNode.IsChecked))
        {
            return;
        }

        // A single check/uncheck can cascade through thousands of nodes;
        // recomputing the selection per node would be quadratic. Skip while a
        // cascade runs and recompute once when it completes.
        if (FolderReviewNode.IsCascadeInProgress)
        {
            return;
        }

        NotifySelectionChanged();
    }

    private void OnNodeCascadeCompleted(object? sender, EventArgs e) => NotifySelectionChanged();

    private void RaiseActionAvailabilityChanged()
    {
        // The scan finishes on a background continuation, but the buttons that
        // listen for CanExecuteChanged are dispatcher-affine, so the refresh is
        // posted to the UI thread when raised from anywhere else.
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(RaiseActionAvailabilityChanged);
            return;
        }

        UploadSelectedFilesCommand.RaiseCanExecuteChanged();
        ZipSelectedItemsCommand.RaiseCanExecuteChanged();
    }

    private bool SetValue<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        RaisePropertyChanged(propertyName!);
        return true;
    }

    private void RaisePropertyChanged(string propertyName)
    {
        // Bindings must observe property changes on the UI thread; a change
        // raised from the scan's background continuation is posted there so
        // no binding is ever left mid-transfer on the wrong thread.
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => OnPropertyChanged(propertyName));
            return;
        }

        OnPropertyChanged(propertyName);
    }

    private void NotifySelectionChanged()
    {
        RaisePropertyChanged(nameof(SelectedFileCount));
        RaisePropertyChanged(nameof(SelectionSummary));
    }
}
