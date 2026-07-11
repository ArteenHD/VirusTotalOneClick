using System.Collections.ObjectModel;
using VirusTotalOneClick.Core;

namespace VirusTotalOneClick.UI;

public sealed class UploadProgressViewModel : ObservableObject
{
    private readonly Func<CancellationToken, Task<FileUploadWorkflowResult>>? _uploadAction;
    private readonly Action<string>? _openResultUrl;
    private readonly Func<string?, bool> _canOpenResultUrl;
    private readonly Action? _closeAction;
    private CancellationTokenSource? _managedUploadCts;
    private Task? _managedUploadTask;
    private string _statusText = "Waiting to upload";
    private int _uploadedCount;
    private bool _isUploadRunning;
    private bool _hasRun;

    public UploadProgressViewModel(
        IReadOnlyList<FileSelection> selections,
        Func<CancellationToken, Task<FileUploadWorkflowResult>>? uploadAction = null,
        bool autoStart = false,
        Action<string>? openResultUrl = null,
        Func<string?, bool>? canOpenResultUrl = null,
        Action? cancelAction = null)
    {
        ArgumentNullException.ThrowIfNull(selections);
        _uploadAction = uploadAction;
        _openResultUrl = openResultUrl;
        _canOpenResultUrl = canOpenResultUrl ?? (url => !string.IsNullOrWhiteSpace(url));
        _closeAction = cancelAction;
        AutoStartsUpload = autoStart;

        Items = new ObservableCollection<UploadQueueItem>(
            selections.Select(selection => new UploadQueueItem(selection.Path)));

        UploadCommand = new AsyncRelayCommand(
            () => StartManagedUploadAsync(CancellationToken.None),
            () => _uploadAction is not null && !_isUploadRunning);
        OpenResultCommand = new RelayCommand(OpenFirstResult, CanOpenFirstResult);
        CancelCommand = new RelayCommand(CancelOrClose);
    }

    public ObservableCollection<UploadQueueItem> Items { get; }

    public AsyncRelayCommand UploadCommand { get; }

    public RelayCommand OpenResultCommand { get; }

    public RelayCommand CancelCommand { get; }

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
            }
        }
    }

    public bool HasRun
    {
        get => _hasRun;
        private set
        {
            if (SetProperty(ref _hasRun, value))
            {
                OnPropertyChanged(nameof(CancelButtonText));
            }
        }
    }

    public string CancelButtonText => _hasRun && !_isUploadRunning ? "Close" : "Cancel";

    public void OpenFirstResult()
    {
        var resultUrl = GetFirstResultUrl();
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
        if (_uploadAction is null)
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
        StatusText = "Uploading...";
        UploadedCount = 0;
        foreach (var item in Items)
        {
            item.Status = "Waiting";
            item.SubmittedName = null;
            item.ResultUrl = null;
            item.ErrorMessage = null;
        }

        RaiseOnUiThread(OpenResultCommand.RaiseCanExecuteChanged);
        Items[0].Status = "Uploading";

        try
        {
            var result = await _uploadAction(cancellationToken).ConfigureAwait(true);
            ApplyResult(result);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Upload canceled.";
            MarkUnresolvedRows("Canceled");
        }
        catch (Exception ex)
        {
            StatusText = $"Upload failed: {ex.Message}";
            foreach (var item in Items)
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

    private void ApplyResult(FileUploadWorkflowResult result)
    {
        if (result.Status == FileUploadWorkflowStatus.MissingApiKey)
        {
            foreach (var item in Items)
            {
                item.Status = "Needs API key";
            }

            StatusText = "API key required before upload.";
            RaiseOnUiThread(OpenResultCommand.RaiseCanExecuteChanged);
            return;
        }

        if (result.ItemResults is { Count: > 0 } itemResults)
        {
            ApplyItemResults(itemResults);
        }
        else
        {
            ApplyAggregateStatus(result);
        }

        ReconcileUnresolvedRows(result);
        UploadedCount = Items.Count(item => item.Status == "Uploaded");

        StatusText = result.Status switch
        {
            FileUploadWorkflowStatus.Completed => $"Uploaded {result.ItemCount} item(s).",
            FileUploadWorkflowStatus.CompletedWithFailures =>
                $"Uploaded {result.ItemCount - result.FailedCount} item(s), {result.FailedCount} failed.",
            FileUploadWorkflowStatus.Canceled => FormatCanceledStatus(result),
            FileUploadWorkflowStatus.Failed => $"Upload failed for {result.FailedCount} item(s).",
            FileUploadWorkflowStatus.NoFiles => "No files to upload.",
            _ => "Upload finished.",
        };

        RaiseOnUiThread(OpenResultCommand.RaiseCanExecuteChanged);
    }

    private void ApplyAggregateStatus(FileUploadWorkflowResult result)
    {
        switch (result.Status)
        {
            case FileUploadWorkflowStatus.Completed:
                foreach (var item in Items)
                {
                    item.Status = "Uploaded";
                }

                break;
            case FileUploadWorkflowStatus.Failed:
                foreach (var item in Items)
                {
                    item.Status = "Failed";
                }

                break;
            case FileUploadWorkflowStatus.CompletedWithFailures:
                var failed = Math.Min(result.FailedCount, Items.Count);
                for (var i = 0; i < Items.Count; i++)
                {
                    Items[i].Status = i < Items.Count - failed ? "Uploaded" : "Failed";
                }

                break;
            case FileUploadWorkflowStatus.Canceled:
                MarkUnresolvedRows("Canceled");
                break;
            default:
                break;
        }
    }

    private void ApplyItemResults(IReadOnlyList<FileUploadWorkflowItemResult> itemResults)
    {
        // Rows are matched positionally per path: duplicate selections each
        // consume their own result instead of colliding on a path-keyed lookup.
        var pathBuckets = Items
            .Select((item, index) => (item, index))
            .GroupBy(pair => pair.item.OriginalPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => new Queue<int>(group.Select(pair => pair.index)),
                StringComparer.OrdinalIgnoreCase);

        foreach (var result in itemResults)
        {
            if (!pathBuckets.TryGetValue(result.OriginalPath, out var queue) || queue.Count == 0)
            {
                continue;
            }

            var item = Items[queue.Dequeue()];
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

    private string FormatCanceledStatus(FileUploadWorkflowResult result)
    {
        var totalCount = result.ItemCount > 0 ? result.ItemCount : TotalCount;
        return $"Upload canceled after {UploadedCount} of {totalCount} item(s) uploaded.";
    }

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
