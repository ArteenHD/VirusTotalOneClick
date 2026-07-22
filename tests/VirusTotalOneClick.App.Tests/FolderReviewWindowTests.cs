using VirusTotalOneClick.Core;
using VirusTotalOneClick.UI;

namespace VirusTotalOneClick.AppTests;

public class FolderReviewWindowTests
{
    private static Func<CancellationToken, Task<FolderScanResult>> ImmediateScan(FolderScanResult result)
        => _ => Task.FromResult(result);

    private static FolderScanResult ScanOf(params string[] files)
        => new(files, Truncated: false, Errors: []);

    private static FileUploadWorkflowResult CompletedResult(int itemCount)
        => new(FileUploadWorkflowStatus.Completed, itemCount);

    [Fact]
    public void OpensImmediatelyInALoadingState()
    {
        StaTestRunner.Run(() =>
        {
            var scanStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseScan = new TaskCompletionSource<FolderScanResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var window = new FolderReviewWindow(@"C:\root", _ =>
            {
                scanStarted.SetResult();
                return releaseScan.Task;
            });
            using var closer = new WindowCloser(window);

            scanStarted.Task.GetAwaiter().GetResult();

            Assert.True(window.ViewModel.IsLoading);
            VisualTreeAssert.AssertWindowContainsText(window, "Scanning folder contents");

            releaseScan.SetResult(ScanOf(@"C:\root\a.txt"));
            window.ScanCompletion.GetAwaiter().GetResult();

            Assert.False(window.ViewModel.IsLoading);
            Assert.Equal(1, window.CandidateFileCount);
        });
    }

    [Fact]
    public void ClosingTheWindowCancelsARunningScan()
    {
        StaTestRunner.Run(() =>
        {
            var scanStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var tokenCanceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var window = new FolderReviewWindow(@"C:\root", token =>
            {
                token.Register(() => tokenCanceled.TrySetResult());
                scanStarted.SetResult();
                return new TaskCompletionSource<FolderScanResult>().Task;
            });

            scanStarted.Task.GetAwaiter().GetResult();
            window.Close();

            Assert.True(tokenCanceled.Task.Wait(TimeSpan.FromSeconds(5)));
        });
    }

    [Fact]
    public void AScanFailureSurfacesAsAScanError()
    {
        StaTestRunner.Run(() =>
        {
            var window = new FolderReviewWindow(
                @"C:\root",
                _ => Task.FromException<FolderScanResult>(new IOException("drive vanished")));
            using var closer = new WindowCloser(window);

            window.ScanCompletion.GetAwaiter().GetResult();

            Assert.False(window.ViewModel.IsLoading);
            Assert.True(window.ViewModel.HasScanErrors);
            Assert.Contains("drive vanished", window.ScanErrorText, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void ScannedFilesBuildATreeMatchingTheFolderNesting()
    {
        StaTestRunner.Run(() =>
        {
            var window = new FolderReviewWindow(
                @"C:\root",
                ImmediateScan(ScanOf(@"C:\root\a.txt", @"C:\root\sub\b.txt", @"C:\root\sub\deep\c.txt")));
            using var closer = new WindowCloser(window);
            window.ScanCompletion.GetAwaiter().GetResult();

            var root = window.RootNode!;
            Assert.Equal(@"C:\root", root.FullPath);
            Assert.Equal(3, window.CandidateFileCount);

            var names = root.Children.Select(child => child.Name).ToList();
            Assert.Contains("a.txt", names);
            Assert.Contains("sub", names);
            var sub = root.Children.Single(child => child.Name == "sub");
            Assert.Contains("b.txt", sub.Children.Select(child => child.Name));
            var deep = sub.Children.Single(child => child.Name == "deep");
            Assert.Contains("c.txt", deep.Children.Select(child => child.Name));
        });
    }

    [Fact]
    public void TheClarityCalloutAndBothActionsAreAlwaysPresent()
    {
        StaTestRunner.Run(() =>
        {
            var window = new FolderReviewWindow(
                @"C:\root",
                ImmediateScan(ScanOf(@"C:\root\a.txt")),
                uploadActionFactory: _ => _ => Task.FromResult(CompletedResult(1)),
                zipUploadActionFactory: _ => _ => Task.FromResult(CompletedResult(1)));
            using var closer = new WindowCloser(window);
            window.ScanCompletion.GetAwaiter().GetResult();

            Assert.Contains("clearer VirusTotal results", window.WarningText, StringComparison.Ordinal);
            VisualTreeAssert.AssertWindowContainsText(window, "Upload selected files");
            VisualTreeAssert.AssertWindowContainsText(window, "Zip selected items and upload");
            VisualTreeAssert.AssertWindowContainsText(window, @"C:\root");
        });
    }

    [Fact]
    public void TheTruncationBannerAppearsOnlyWhenTruncatedAndNamesTheLimit()
    {
        StaTestRunner.Run(() =>
        {
            var truncated = new FolderReviewWindow(
                @"C:\root",
                ImmediateScan(new FolderScanResult([@"C:\root\a.txt"], Truncated: true, Errors: [])),
                scanFileLimit: 250);
            using var closerA = new WindowCloser(truncated);
            truncated.ScanCompletion.GetAwaiter().GetResult();

            Assert.True(truncated.ViewModel.HasTruncationWarning);
            Assert.Contains("first 250 files", truncated.ViewModel.TruncationWarningText, StringComparison.Ordinal);

            var complete = new FolderReviewWindow(@"C:\root", ImmediateScan(ScanOf(@"C:\root\a.txt")));
            using var closerB = new WindowCloser(complete);
            complete.ScanCompletion.GetAwaiter().GetResult();

            Assert.False(complete.ViewModel.HasTruncationWarning);
            Assert.Equal(string.Empty, complete.ViewModel.TruncationWarningText);
        });
    }

    [Fact]
    public void PerItemScanErrorsSurfaceWithTheirText()
    {
        StaTestRunner.Run(() =>
        {
            var window = new FolderReviewWindow(
                @"C:\root",
                ImmediateScan(new FolderScanResult(
                    [@"C:\root\a.txt"], Truncated: false, Errors: ["locked: secret.bin", "denied: vault"])));
            using var closer = new WindowCloser(window);
            window.ScanCompletion.GetAwaiter().GetResult();

            Assert.True(window.ViewModel.HasScanErrors);
            Assert.StartsWith("2 items could not be scanned: ", window.ScanErrorText, StringComparison.Ordinal);
            Assert.Contains("locked: secret.bin; denied: vault", window.ScanErrorText, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void UncheckingALeafLiveUpdatesTheCountAndSummary()
    {
        StaTestRunner.Run(() =>
        {
            using var folder = new TemporaryFolder();
            var fileA = folder.CreateFile("a.txt", "aaaa");
            var fileB = folder.CreateFile("b.txt", "bb");
            var window = new FolderReviewWindow(folder.Path, ImmediateScan(ScanOf(fileA, fileB)));
            using var closer = new WindowCloser(window);
            window.ScanCompletion.GetAwaiter().GetResult();

            var raised = new List<string>();
            window.ViewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

            Assert.Equal(2, window.SelectedFileCount);
            var leafA = window.RootNode!.SelectedFiles().First(node => node.FullPath == fileA);
            leafA.IsChecked = false;

            Assert.Equal(1, window.SelectedFileCount);
            Assert.Contains(nameof(FolderReviewViewModel.SelectedFileCount), raised);
            Assert.Contains(nameof(FolderReviewViewModel.SelectionSummary), raised);
            Assert.StartsWith("1 file selected · Total size: 2 B", window.ViewModel.SelectionSummary, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void CheckedStateSetBeforeTheVisualTreeExistsStaysConsistent()
    {
        StaTestRunner.Run(() =>
        {
            // Selection state must be pure viewmodel state: changing it before
            // any item container or visual tree exists must survive rendering.
            var window = new FolderReviewWindow(
                @"C:\root",
                ImmediateScan(ScanOf(@"C:\root\a.txt", @"C:\root\b.txt")));
            using var closer = new WindowCloser(window);
            window.ScanCompletion.GetAwaiter().GetResult();

            window.RootNode!.IsChecked = false;

            Assert.Equal(0, window.SelectedFileCount);
            VisualTreeAssert.AssertWindowContainsText(window, "0 files selected");
        });
    }

    [Fact]
    public void UploadRunsTheComposedActionWithExactlyTheCheckedFiles()
    {
        StaTestRunner.Run(() =>
        {
            IReadOnlyList<string>? actionPaths = null;
            var window = new FolderReviewWindow(
                @"C:\root",
                ImmediateScan(ScanOf(@"C:\root\keep.txt", @"C:\root\drop.txt")),
                uploadActionFactory: paths =>
                {
                    actionPaths = paths;
                    return _ => Task.FromResult(CompletedResult(paths.Count));
                });
            using var closer = new WindowCloser(window);
            window.ScanCompletion.GetAwaiter().GetResult();

            var drop = window.RootNode!.SelectedFiles().First(node => node.Name == "drop.txt");
            drop.IsChecked = false;
            window.UploadSelectedFilesAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.Equal([@"C:\root\keep.txt"], actionPaths);
            Assert.Equal("Uploaded 1 item(s).", window.ActionStatusText);
        });
    }

    [Fact]
    public void AnEmptySelectionIsANoOp()
    {
        StaTestRunner.Run(() =>
        {
            var ran = false;
            var window = new FolderReviewWindow(
                @"C:\root",
                ImmediateScan(ScanOf(@"C:\root\a.txt")),
                uploadActionFactory: _ => _ => { ran = true; return Task.FromResult(CompletedResult(1)); });
            using var closer = new WindowCloser(window);
            window.ScanCompletion.GetAwaiter().GetResult();

            window.RootNode!.IsChecked = false;
            window.UploadSelectedFilesAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.False(ran);
            Assert.Equal("No files selected.", window.ActionStatusText);
        });
    }

    [Fact]
    public void UploadHandsOffToTheProgressOpenerWithAutoStart()
    {
        StaTestRunner.Run(() =>
        {
            IReadOnlyList<FileSelection>? handedSelections = null;
            var handedAutoStart = false;
            var handoffCompleted = false;
            Func<IReadOnlyList<string>, Func<CancellationToken, Task<FileUploadWorkflowResult>>>? handedFactory = null;
            var window = new FolderReviewWindow(
                @"C:\root",
                ImmediateScan(ScanOf(@"C:\root\a.txt")),
                uploadActionFactory: paths => _ => Task.FromResult(CompletedResult(paths.Count)),
                openUploadProgress: (selections, _, autoStart, uploadActionFactory) =>
                {
                    handedSelections = selections;
                    handedAutoStart = autoStart;
                    handedFactory = uploadActionFactory;
                },
                progressHandoffCompletedAction: () => handoffCompleted = true);
            using var closer = new WindowCloser(window);
            window.ScanCompletion.GetAwaiter().GetResult();

            window.UploadSelectedFilesAsync(CancellationToken.None).GetAwaiter().GetResult();

            var selection = Assert.Single(handedSelections!);
            Assert.Equal(@"C:\root\a.txt", selection.Path);
            Assert.False(selection.IsDirectory);
            Assert.True(handedAutoStart);
            Assert.True(handoffCompleted);
            Assert.Equal("Upload opened in progress window.", window.ActionStatusText);

            // The file hand-off keeps a per-path retry seam so a later
            // retry can resubmit only what has not uploaded yet.
            Assert.NotNull(handedFactory);
            var retryAction = handedFactory!([@"C:\root\a.txt"]);
            var retryResult = retryAction(CancellationToken.None).GetAwaiter().GetResult();
            Assert.Equal(1, retryResult.ItemCount);
        });
    }

    [Fact]
    public void ZipHandsOffASingleRootSelectionAndTheRootedRequest()
    {
        StaTestRunner.Run(() =>
        {
            FolderZipUploadRequest? request = null;
            IReadOnlyList<FileSelection>? handedSelections = null;
            Func<IReadOnlyList<string>, Func<CancellationToken, Task<FileUploadWorkflowResult>>>? handedFactory = null;
            var window = new FolderReviewWindow(
                @"C:\root",
                ImmediateScan(ScanOf(@"C:\root\a.txt", @"C:\root\sub\b.txt")),
                zipUploadActionFactory: zipRequest =>
                {
                    request = zipRequest;
                    return _ => Task.FromResult(CompletedResult(1));
                },
                openUploadProgress: (selections, _, _, uploadActionFactory) =>
                {
                    handedSelections = selections;
                    handedFactory = uploadActionFactory;
                });
            using var closer = new WindowCloser(window);
            window.ScanCompletion.GetAwaiter().GetResult();

            window.ZipSelectedItemsAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.Equal(@"C:\root", request!.SourceRootPath);
            Assert.Equal(2, request.SelectedFiles.Count);
            var selection = Assert.Single(handedSelections!);
            Assert.Equal(@"C:\root", selection.Path);
            Assert.True(selection.IsDirectory);
            Assert.Equal("Zip upload opened in progress window.", window.ActionStatusText);

            // The zip bundles the whole selection into one upload; there is
            // no per-path subset to retry, so no retry seam is offered.
            Assert.Null(handedFactory);
        });
    }

    [Fact]
    public void InlineRunsDistinguishTotalFromPartialFailure()
    {
        StaTestRunner.Run(() =>
        {
            var window = new FolderReviewWindow(
                @"C:\root",
                ImmediateScan(ScanOf(@"C:\root\a.txt", @"C:\root\b.txt")),
                uploadActionFactory: _ => _ => Task.FromResult(
                    new FileUploadWorkflowResult(FileUploadWorkflowStatus.CompletedWithFailures, 2, 1)));
            using var closer = new WindowCloser(window);
            window.ScanCompletion.GetAwaiter().GetResult();

            window.UploadSelectedFilesAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.Equal("Uploaded 1 item(s), 1 failed.", window.ActionStatusText);

            var failedWindow = new FolderReviewWindow(
                @"C:\root",
                ImmediateScan(ScanOf(@"C:\root\a.txt")),
                uploadActionFactory: _ => _ => Task.FromResult(
                    new FileUploadWorkflowResult(FileUploadWorkflowStatus.Failed, 1, 1)));
            using var closerB = new WindowCloser(failedWindow);
            failedWindow.ScanCompletion.GetAwaiter().GetResult();

            failedWindow.UploadSelectedFilesAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.Equal("Upload failed for 1 item(s).", failedWindow.ActionStatusText);
        });
    }

    [Fact]
    public void TheDefaultActionFollowsTheConfiguredFolderMode()
    {
        StaTestRunner.Run(() =>
        {
            var zipPrimary = new FolderReviewWindow(
                @"C:\root",
                ImmediateScan(ScanOf(@"C:\root\a.txt")),
                defaultFolderMode: FolderUploadMode.ReviewThenZip);
            using var closerA = new WindowCloser(zipPrimary);
            zipPrimary.ScanCompletion.GetAwaiter().GetResult();

            Assert.Equal("Zip selected items and upload", zipPrimary.DefaultActionText);
            Assert.Equal(
                Wpf.Ui.Controls.ControlAppearance.Primary,
                zipPrimary.ZipButton.Appearance);
            Assert.Equal(
                Wpf.Ui.Controls.ControlAppearance.Secondary,
                zipPrimary.UploadButton.Appearance);

            var uploadPrimary = new FolderReviewWindow(@"C:\root", ImmediateScan(ScanOf(@"C:\root\a.txt")));
            using var closerB = new WindowCloser(uploadPrimary);
            uploadPrimary.ScanCompletion.GetAwaiter().GetResult();

            Assert.Equal("Upload selected files", uploadPrimary.DefaultActionText);
            Assert.Equal(
                Wpf.Ui.Controls.ControlAppearance.Primary,
                uploadPrimary.UploadButton.Appearance);
        });
    }

    [Fact]
    public void CancelClosesWithoutUploading()
    {
        StaTestRunner.Run(() =>
        {
            var uploaded = false;
            var window = new FolderReviewWindow(
                @"C:\root",
                ImmediateScan(ScanOf(@"C:\root\a.txt")),
                uploadActionFactory: _ => _ => { uploaded = true; return Task.FromResult(CompletedResult(1)); });
            window.ScanCompletion.GetAwaiter().GetResult();

            window.ViewModel.CancelCommand.Execute(null);

            Assert.False(uploaded);
            Assert.False(window.IsVisible);
        });
    }

    [Fact]
    public void ActionsAreUnavailableWhileTheScanIsStillRunning()
    {
        StaTestRunner.Run(() =>
        {
            var releaseScan = new TaskCompletionSource<FolderScanResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var window = new FolderReviewWindow(
                @"C:\root",
                _ => releaseScan.Task,
                uploadActionFactory: _ => _ => Task.FromResult(CompletedResult(1)));
            using var closer = new WindowCloser(window);

            Assert.False(window.ViewModel.UploadSelectedFilesCommand.CanExecute(null));

            releaseScan.SetResult(ScanOf(@"C:\root\a.txt"));
            window.ScanCompletion.GetAwaiter().GetResult();

            Assert.True(window.ViewModel.UploadSelectedFilesCommand.CanExecute(null));
        });
    }
}
