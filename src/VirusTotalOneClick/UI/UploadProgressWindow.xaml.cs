using System.Windows;
using VirusTotalOneClick.Core;

namespace VirusTotalOneClick.UI;

public partial class UploadProgressWindow : AppWindow
{
    private readonly UploadProgressViewModel _viewModel;
    private readonly bool _autoStart;
    private bool _autoStartTriggered;

    public UploadProgressWindow(
        IReadOnlyList<FileSelection> selections,
        Func<CancellationToken, Task<FileUploadWorkflowResult>>? uploadAction = null,
        bool autoStart = false,
        bool isDevMode = false,
        Action<string>? openResultUrl = null,
        Func<string?, bool>? canOpenResultUrl = null,
        Action? openSettings = null,
        Func<IReadOnlyList<string>, Func<CancellationToken, Task<FileUploadWorkflowResult>>>? uploadActionFactory = null)
    {
        IsDevMode = isDevMode;
        _autoStart = autoStart;
        _viewModel = new UploadProgressViewModel(
            selections,
            uploadAction,
            autoStart,
            openResultUrl,
            canOpenResultUrl,
            cancelAction: Close,
            openSettings: openSettings,
            uploadActionFactory: uploadActionFactory);
        DataContext = _viewModel;
        InitializeComponent();

        if (Content is UIElement content)
        {
            content.Measure(new Size(Width, Height));
            content.Arrange(new Rect(0, 0, Width, Height));
        }

        Loaded += OnLoaded;
    }

    public bool IsDevMode { get; }

    public bool AutoStartsUpload => _autoStart;

    public UploadProgressViewModel ViewModel => _viewModel;

    public string StatusText => _viewModel.StatusText;

    public string FirstQueueItemStatus => _viewModel.Items.Count > 0 ? _viewModel.Items[0].Status : string.Empty;

    public IReadOnlyList<string> QueueItemStatuses => _viewModel.Items.Select(item => item.Status).ToList();

    public string? SecondQueueItemResultUrl => _viewModel.Items.Count > 1 ? _viewModel.Items[1].ResultUrl : null;

    public Task RunUploadAsync(CancellationToken cancellationToken) => _viewModel.RunUploadAsync(cancellationToken);

    public void OpenFirstResult() => _viewModel.OpenFirstResult();

    /// <summary>
    /// Starts the auto-start upload if it is armed and has not fired yet.
    /// Returns null when auto-start is off or already consumed — the guard
    /// that keeps a double Loaded (or a manual call) from uploading twice.
    /// </summary>
    public Task? TryTriggerAutoStart()
    {
        if (!_autoStart || _autoStartTriggered)
        {
            return null;
        }

        _autoStartTriggered = true;
        return _viewModel.StartManagedUploadAsync(CancellationToken.None);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.CancelAndDisposeManagedUpload();
        base.OnClosed(e);
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => _ = TryTriggerAutoStart();
}
