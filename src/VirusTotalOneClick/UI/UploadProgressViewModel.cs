using System.Collections.ObjectModel;
using VirusTotalOneClick.Core;

namespace VirusTotalOneClick.UI;

public sealed class UploadProgressViewModel : ObservableObject
{
    private readonly Func<CancellationToken, Task<FileUploadWorkflowResult>>? _uploadAction;
    private readonly Func<IReadOnlyList<string>, Func<CancellationToken, Task<FileUploadWorkflowResult>>>? _uploadActionFactory;
    private readonly Action<string>? _openResultUrl;
    private readonly Func<string?, bool> _canOpenResultUrl;
    private readonly Action? _closeAction;
    private readonly Action? _openSettings;
    private CancellationTokenSource? _managedUploadCts;
    private Task? _managedUploadTask;
    private string _statusText = "Waiting to upload";
    private int _uploadedCount;
    private bool _isUploadRunning;
    private bool _isIndeterminate;
    private bool _hasRun;

    public UploadProgressViewModel(
        IReadOnlyList<FileSelection> selections,
        Func<CancellationToken, Task<FileUploadWorkflowResult>>? uploadAction = null,
        bool autoStart = false,
        Action<string>? openResultUrl = null,
        Func<string?, bool>? canOpenResultUrl = null,
        Action? cancelAction = null,
        Action? openSettings = null,
        Func<IReadOnlyList<string>, Func<CancellationToken, Task<FileUploadWorkflowResult>>>? uploadActionFactory = null)
    {
        ArgumentNullException.ThrowIfNull(selections);
        _uploadAction = uploadAction;
        _uploadActionFactory = uploadActionFactory;
        _openResultUrl = openResultUrl;
        _canOpenResultUrl = canOpenResultUrl ?? (url => !string.IsNullOrWhiteSpace(url));
        _closeAction = cancelAction;
        _openSettings = openSettings;
        AutoStartsUpload = autoStart;

        Items = new ObservableCollection<UploadQueueItem>(
            selections.Select(selection => new UploadQueueItem(selection.Path)));

        UploadCommand = new AsyncRelayCommand(
            () => StartManagedUploadAsync(CancellationToken.None),
            () => (_uploadAction is not null || _uploadActionFactory is not null) && !_isUploadRunning);
        OpenResultCommand = new RelayCommand(OpenFirstResult, CanOpenFirstResult);
        CancelCommand = new RelayCommand(CancelOrClose);
        OpenSettingsCommand = new RelayCommand(() => _openSettings?.Invoke(), () => _openSettings is not null);
    }

    public ObservableCollection<UploadQueueItem> Items { get; }

    public AsyncRelayCommand UploadCommand { get; }

    public RelayCommand OpenResultCommand { get; }

    public RelayCommand CancelCommand { get; }

    public RelayCommand OpenSettingsCommand { get; }

    public bool AutoStartsUpload { get; }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public int UploadedCount
    {
        get => _uploadedCount;
        private set
        {
            if (SetProperty(ref _uploadedCount, value))
            {
                OnPropertyChanged(nameof(ProgressFraction));
                OnPropertyChanged(nameof(ProgressSummary));
                OnPropertyChanged(nameof(ProgressPercent));
            }
        }
    }

    public int TotalCount => Items.Count;

    public double ProgressFraction => TotalCount == 0 ? 0 : (double)_uploadedCount / TotalCount;

    public string ProgressSummary
    {
        get
        {
            if (!IsUploadRunning)
            {
                var failedFinal = Items.Count(item => item.Status is "Failed" or "Needs API key");
                var canceledFinal = Items.Count(item => item.Status == "Canceled");
                if (canceledFinal > 0)
                {
                    var finalParts = new List<string>();
                    if (_uploadedCount > 0)
                    {
                        finalParts.Add($"{_uploadedCount} uploaded");
                    }

                    if (failedFinal > 0)
                    {
                        finalParts.Add($"{failedFinal} failed");
                    }

                    finalParts.Add($"{canceledFinal} canceled");
                    return string.Join(" · ", finalParts);
                }

                if (failedFinal > 0 && _uploadedCount > 0)
                {
                    return $"{_uploadedCount} uploaded · {failedFinal} failed";
                }

                if (failedFinal > 0)
                {
                    return $"{failedFinal} of {TotalCount} failed";
                }

                return $"{_uploadedCount} of {TotalCount} uploaded";
            }

            var queued = Items.Count(item => item.Status == "Waiting");
            var failed = Items.Count(item => item.Status is "Failed" or "Needs API key");
            var parts = new List<string> { $"Uploading {_uploadedCount} of {TotalCount} item(s)" };
            if (queued > 0)
            {
                parts.Add($"{queued} in queue");
            }

            if (failed > 0)
            {
                parts.Add($"{failed} failed");
            }

            return string.Join(" · ", parts);
        }
    }

    public string ProgressPercent => TotalCount == 0 ? "0%" : $"{(int)(ProgressFraction * 100)}%";

    public bool IsUploadRunning
    {
        get => _isUploadRunning;
        private set
        {
            if (SetProperty(ref _isUploadRunning, value))
            {
                RaiseOnUiThread(UploadCommand.RaiseCanExecuteChanged);
                OnPropertyChanged(nameof(ProgressSummary));
                OnPropertyChanged(nameof(ProgressPercent));
                OnPropertyChanged(nameof(CancelButtonText));
                OnPropertyChanged(nameof(ShowUploadButton));
                OnPropertyChanged(nameof(IsOpenResultPrimary));
                OnPropertyChanged(nameof(ShowOpenSettings));
            }
        }
    }

    /// <summary>
    /// True for the duration of a run: per-item results only land when the
    /// action resolves, so a determinate bar would sit dead at 0% the whole
    /// time. The bar goes indeterminate instead and returns to a determinate
    /// summary once the run settles.
    /// </summary>
    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        private set => SetProperty(ref _isIndeterminate, value);
    }

    public bool HasRun
    {
        get => _hasRun;
        private set
        {
            if (SetProperty(ref _hasRun, value))
            {
                OnPropertyChanged(nameof(CancelButtonText));
                OnPropertyChanged(nameof(ShowUploadButton));
                OnPropertyChanged(nameof(IsOpenResultPrimary));
                OnPropertyChanged(nameof(ShowOpenSettings));
            }
        }
    }

    public string CancelButtonText => _hasRun && !_isUploadRunning ? "Close" : "Cancel";

    /// <summary>
    /// False only once a run has settled with every row uploaded — re-running
    /// could change nothing, so the button leaves the stage to "Open result".
    /// </summary>
    public bool ShowUploadButton =>
        !_hasRun || _isUploadRunning || Items.Any(item => item.Status != "Uploaded");

    /// <summary>
    /// True when a settled run produced at least one result link; the footer
    /// promotes "Open result" to the primary action.
    /// </summary>
    public bool IsOpenResultPrimary =>
        _hasRun && !_isUploadRunning && Items.Any(item => item.HasResultUrl);

    /// <summary>
    /// True once a run has settled with at least one row needing an API key;
    /// the footer offers a direct path to Settings instead of leaving the
    /// user to find it on their own.
    /// </summary>
    public bool ShowOpenSettings =>
        _hasRun && !_isUploadRunning && Items.Any(item => item.Status == "Needs API key");

    public void OpenFirstResult() => OpenResultUrl(GetFirstResultUrl());

    public void OpenRowResult(UploadQueueItem? row) => OpenResultUrl(row?.ResultUrl);

    private void OpenResultUrl(string? resultUrl)
    {
        if (_openResultUrl is null || resultUrl is null || !_canOpenResultUrl(resultUrl))
        {
            return;
        }

        try
        {
            _openResultUrl(resultUrl);
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    public void CancelAndDisposeManagedUpload()
    {
        var cts = _managedUploadCts;
        _managedUploadCts = null;
        if (cts is null)
        {
            return;
        }

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        cts.Dispose();
    }

    public Task StartManagedUploadAsync(CancellationToken cancellationToken)
    {
        if (_managedUploadTask is not null && !_managedUploadTask.IsCompleted)
        {
            return _managedUploadTask;
        }

        _managedUploadCts?.Dispose();
        _managedUploadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _managedUploadTask = RunUploadAsync(_managedUploadCts.Token);
        return _managedUploadTask;
    }

    public async Task RunUploadAsync(CancellationToken cancellationToken)
    {
        // A retry recreates the action for only the rows that have not
        // uploaded yet, so an already-uploaded row is never resubmitted.
        // The very first run has nothing uploaded yet, so it naturally
        // covers every row; only a factory-backed retry takes this path,
        // otherwise the single fixed action (e.g. the zip hand-off) keeps
        // running exactly as before.
        var isRetry = _hasRun && _uploadActionFactory is not null;
        var activeItems = isRetry
            ? Items.Where(item => item.Status != "Uploaded").ToList()
            : Items.ToList();

        var action = isRetry
            ? _uploadActionFactory!(activeItems.Select(item => item.OriginalPath).ToList())
            : _uploadAction;

        if (action is null)
        {
            StatusText = "No upload action configured.";
            return;
        }

        if (Items.Count == 0)
        {
            StatusText = "No files to upload.";
            return;
        }

        IsUploadRunning = true;
        IsIndeterminate = true;
        StatusText = "Uploading...";
        foreach (var item in activeItems)
        {
            item.Status = "Waiting";
            item.SubmittedName = null;
            item.ResultUrl = null;
            item.ErrorMessage = null;
        }

        UploadedCount = Items.Count(item => item.Status == "Uploaded");
        RaiseOnUiThread(OpenResultCommand.RaiseCanExecuteChanged);
        if (activeItems.Count > 0)
        {
            activeItems[0].Status = "Uploading";
        }

        try
        {
            var result = await action(cancellationToken).ConfigureAwait(true);
            ApplyResult(result, activeItems);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Upload canceled.";
            MarkUnresolvedRows("Canceled");
        }
        catch (Exception ex)
        {
            StatusText = $"Upload failed: {ex.Message}";
            foreach (var item in activeItems)
            {
                if (item.Status is "Uploading" or "Waiting")
                {
                    item.Status = "Failed";
                    item.ErrorMessage = ex.Message;
                }
            }
        }
        finally
        {
            IsUploadRunning = false;
            IsIndeterminate = false;
            HasRun = true;
            RaiseOnUiThread(UploadCommand.RaiseCanExecuteChanged);
            RaiseOnUiThread(OpenResultCommand.RaiseCanExecuteChanged);
        }
    }

    private void CancelOrClose()
    {
        if (IsUploadRunning)
        {
            StatusText = "Canceling upload...";
            _managedUploadCts?.Cancel();
            return;
        }

        _closeAction?.Invoke();
    }

    private void ApplyResult(FileUploadWorkflowResult result, IReadOnlyList<UploadQueueItem> activeItems)
    {
        if (result.Status == FileUploadWorkflowStatus.MissingApiKey)
        {
            foreach (var item in activeItems)
            {
                item.Status = "Needs API key";
            }

            StatusText = "API key required before upload.";
            RaiseOnUiThread(OpenResultCommand.RaiseCanExecuteChanged);
            return;
        }

        if (result.ItemResults is { Count: > 0 } itemResults)
        {
            ApplyItemResults(itemResults, activeItems);
        }
        else
        {
            ApplyAggregateStatus(result, activeItems);
        }

        ReconcileUnresolvedRows(result);
        UploadedCount = Items.Count(item => item.Status == "Uploaded");

        StatusText = result.Status switch
        {
            FileUploadWorkflowStatus.Completed => $"Uploaded {result.ItemCount} item(s).",
            FileUploadWorkflowStatus.CompletedWithFailures =>
                $"Uploaded {result.ItemCount - result.FailedCount} item(s), {result.FailedCount} failed.",
            FileUploadWorkflowStatus.Canceled => FormatCanceledStatus(),
            FileUploadWorkflowStatus.Failed => $"Upload failed for {result.FailedCount} item(s).",
            FileUploadWorkflowStatus.NoFiles => "No files to upload.",
            _ => "Upload finished.",
        };

        RaiseOnUiThread(OpenResultCommand.RaiseCanExecuteChanged);
    }

    private void ApplyAggregateStatus(FileUploadWorkflowResult result, IReadOnlyList<UploadQueueItem> activeItems)
    {
        switch (result.Status)
        {
            case FileUploadWorkflowStatus.Completed:
                foreach (var item in activeItems)
                {
                    item.Status = "Uploaded";
                }

                break;
            case FileUploadWorkflowStatus.Failed:
                foreach (var item in activeItems)
                {
                    item.Status = "Failed";
                }

                break;
            case FileUploadWorkflowStatus.CompletedWithFailures:
                var failed = Math.Min(result.FailedCount, activeItems.Count);
                for (var i = 0; i < activeItems.Count; i++)
                {
                    activeItems[i].Status = i < activeItems.Count - failed ? "Uploaded" : "Failed";
                }

                break;
            case FileUploadWorkflowStatus.Canceled:
                MarkUnresolvedRows("Canceled");
                break;
            default:
                break;
        }
    }

    private static void ApplyItemResults(
        IReadOnlyList<FileUploadWorkflowItemResult> itemResults, IReadOnlyList<UploadQueueItem> activeItems)
    {
        // Rows are matched positionally per path within the active set:
        // duplicate selections each consume their own result instead of
        // colliding on a path-keyed lookup, and a row already resolved from
        // an earlier run (excluded from activeItems) is never touched.
        var pathBuckets = activeItems
            .GroupBy(item => item.OriginalPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => new Queue<UploadQueueItem>(group),
                StringComparer.OrdinalIgnoreCase);

        foreach (var result in itemResults)
        {
            if (!pathBuckets.TryGetValue(result.OriginalPath, out var queue) || queue.Count == 0)
            {
                continue;
            }

            var item = queue.Dequeue();
            item.SubmittedName = result.SubmittedName;
            item.ResultUrl = result.ResultUrl;
            item.ErrorMessage = result.ErrorMessage;
            item.Status = NormalizeStatus(result.Status);
        }
    }

    private static string NormalizeStatus(string? raw) => raw?.ToLowerInvariant() switch
    {
        "uploaded" => "Uploaded",
        "failed" => "Failed",
        "canceled" => "Canceled",
        "uploading" => "Uploading",
        "waiting" => "Waiting",
        "needs api key" => "Needs API key",
        null => "Waiting",
        _ => raw,
    };

    private void ReconcileUnresolvedRows(FileUploadWorkflowResult result)
    {
        if (result.Status == FileUploadWorkflowStatus.Canceled)
        {
            MarkUnresolvedRows("Canceled");
            return;
        }

        if (result.Status == FileUploadWorkflowStatus.NoFiles || result.ItemResults is { Count: > 0 })
        {
            MarkUnresolvedRows("Failed", FileUploadWorkflow.MissingFileErrorMessage);
        }
    }

    private void MarkUnresolvedRows(string status, string? errorMessage = null)
    {
        foreach (var item in Items.Where(item => item.Status is "Uploading" or "Waiting"))
        {
            item.Status = status;
            item.ErrorMessage = errorMessage;
        }
    }

    /// <summary>
    /// Both numbers here read from the whole queue, matching ProgressSummary:
    /// result.ItemCount is only the scope of whatever ran this time (every
    /// row on a first run, but just the retried subset after a partial
    /// retry), so pairing it with the queue-wide UploadedCount would mix
    /// scopes and read as inconsistent with the rows actually shown.
    /// </summary>
    private string FormatCanceledStatus() =>
        $"Upload canceled after {UploadedCount} of {TotalCount} item(s) uploaded.";

    private bool CanOpenFirstResult() =>
        _openResultUrl is not null && _canOpenResultUrl(GetFirstResultUrl());

    private string? GetFirstResultUrl() =>
        Items.FirstOrDefault(item => item.HasResultUrl)?.ResultUrl;

    // The upload action's continuation can resume off the UI thread when the
    // awaited task completes from another thread (no captured dispatcher
    // synchronization context). Raising ICommand.CanExecuteChanged directly
    // there would let a bound Button synchronously read its Command dependency
    // property from the wrong thread and crash, so the raise is deferred to
    // the UI thread via BeginInvoke (never a blocking Invoke, which could
    // deadlock against a caller that is itself waiting on this same task).
    private static void RaiseOnUiThread(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.BeginInvoke(action);
        }
    }
}
