using System.IO;
using System.Windows;
using VirusTotalOneClick.Core;

namespace VirusTotalOneClick.UI;

/// <summary>
/// Builds fully wired windows from launch options. All stores, workflows, and
/// policies come in through the composition; this type only routes and wires.
/// </summary>
public sealed class WindowFactory
{
    private readonly AppComposition _composition;
    private readonly FolderScanOptions _folderScanOptions;
    private readonly Action<string> _openResultUrl;
    private readonly Func<string?, bool> _canOpenResultUrl;

    public WindowFactory(
        AppComposition composition,
        FolderScanOptions? folderScanOptions = null,
        Action<string>? openResultUrl = null,
        Func<string?, bool>? canOpenResultUrl = null)
    {
        ArgumentNullException.ThrowIfNull(composition);
        var launcher = new ExternalUrlLauncher();
        _composition = composition;
        _folderScanOptions = folderScanOptions ?? new FolderScanOptions(MaxFiles: 5000);
        _openResultUrl = openResultUrl ?? launcher.Open;
        _canOpenResultUrl = canOpenResultUrl ?? launcher.CanOpen;
    }

    public Window Create(CommandLineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var isDevMode = _composition.ApiKeyValidator.IsTestDevKey(
            _composition.SecretStore.ReadApiKey() ?? string.Empty);

        return options.Mode switch
        {
            AppMode.Settings => new SettingsWindow(
                _composition.Settings,
                _composition.SaveSettings,
                _composition.SecretStore,
                _composition.ClearHistory,
                _composition.ApplyIntegration,
                CreateOpenHistoryAction(),
                _composition.ValidateApiKey,
                _composition.ApiKeyValidator),
            AppMode.History => CreateHistoryWindow(),
            AppMode.Upload => CreateUploadWindow(options.Paths, isDevMode),
            _ => new SetupWindow(
                _composition.Settings,
                _composition.SaveSettings,
                _composition.SecretStore,
                _composition.ApplyIntegration,
                _composition.ValidateApiKey,
                _composition.ApiKeyValidator),
        };
    }

    private HistoryWindow CreateHistoryWindow()
        => new(
            _composition.ReadHistory(),
            openResultUrl: _openResultUrl,
            canOpenResultUrl: _canOpenResultUrl,
            clearHistory: _composition.ClearHistory,
            keepHistoryEnabled: _composition.Settings.KeepLocalHistory);

    private Action CreateOpenHistoryAction()
        => () => CreateHistoryWindow().Show();

    private Window CreateUploadWindow(IReadOnlyList<string> paths, bool isDevMode)
    {
        var selections = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => new FileSelection(path, Directory.Exists(path)))
            .ToList();

        var plan = UploadPlanner.Plan(selections, _composition.Settings.ConfirmBeforeFileUpload);
        if (plan.RequiresFolderReview)
        {
            return CreateFolderReviewWindow(selections, isDevMode);
        }

        var filePaths = selections.Select(selection => selection.Path).ToList();
        return new UploadProgressWindow(
            selections,
            _composition.CreateUploadAction(filePaths),
            autoStart: plan.CanUploadImmediately,
            isDevMode: isDevMode,
            openResultUrl: _openResultUrl,
            canOpenResultUrl: _canOpenResultUrl);
    }

    private FolderReviewWindow CreateFolderReviewWindow(IReadOnlyList<FileSelection> selections, bool isDevMode)
    {
        var reviewRoot = ReviewRootLocator.FindCommonRoot(selections);

        // The review window opens immediately and runs this scan off the UI
        // thread with its own cancellation; closing the window cancels it.
        Task<FolderScanResult> ScanAsync(CancellationToken cancellationToken)
            => Task.FromResult(BuildReviewScanResult(selections, cancellationToken));

        FolderReviewWindow? reviewWindow = null;
        void OpenProgress(
            IReadOnlyList<FileSelection> handedSelections,
            Func<CancellationToken, Task<FileUploadWorkflowResult>> action,
            bool autoStart)
        {
            var progress = new UploadProgressWindow(
                handedSelections,
                action,
                autoStart,
                isDevMode,
                _openResultUrl,
                _canOpenResultUrl);
            progress.Show();
        }

        void CloseReviewAfterHandoff()
        {
            if (reviewWindow is { IsVisible: true })
            {
                reviewWindow.Close();
            }
        }

        reviewWindow = new FolderReviewWindow(
            reviewRoot,
            ScanAsync,
            _composition.CreateUploadAction,
            _composition.CreateZipUploadAction,
            _composition.Settings.DefaultFolderMode,
            isDevMode,
            openUploadProgress: OpenProgress,
            progressHandoffCompletedAction: CloseReviewAfterHandoff,
            scanFileLimit: _folderScanOptions.MaxFiles);
        return reviewWindow;
    }

    private FolderScanResult BuildReviewScanResult(
        IReadOnlyList<FileSelection> selections,
        CancellationToken cancellationToken)
    {
        if (_folderScanOptions.MaxFiles <= 0)
        {
            return new FolderScanResult([], Truncated: true, []);
        }

        var files = new List<string>();
        var errors = new List<string>();
        var truncated = false;

        foreach (var selection in selections)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remainingFileCount = _folderScanOptions.MaxFiles - files.Count;
            if (remainingFileCount <= 0)
            {
                truncated = true;
                break;
            }

            if (selection.IsDirectory)
            {
                var result = FolderScanner.Scan(
                    selection.Path,
                    _folderScanOptions with { MaxFiles = remainingFileCount },
                    cancellationToken);
                files.AddRange(result.Files);
                errors.AddRange(result.Errors);
                truncated |= result.Truncated;
                continue;
            }

            files.Add(selection.Path);
        }

        return new FolderScanResult(files, truncated, errors);
    }
}
