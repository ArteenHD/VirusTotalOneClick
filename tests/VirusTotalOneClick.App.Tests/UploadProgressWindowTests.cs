using VirusTotalOneClick.Core;
using VirusTotalOneClick.UI;

namespace VirusTotalOneClick.AppTests;

public class UploadProgressWindowTests
{
    private static List<FileSelection> Files(params string[] paths)
        => paths.Select(path => new FileSelection(path, IsDirectory: false)).ToList();

    private static FileUploadWorkflowItemResult Item(
        string path, string status, string? url = null, string? error = null)
        => new(path, Path.GetFileName(path), status, url, error);

    [Fact]
    public void MissingApiKeyMarksEveryRowAndTheStatusText()
    {
        StaTestRunner.Run(() =>
        {
            var window = new UploadProgressWindow(
                Files(@"C:\a.txt", @"C:\b.txt"),
                _ => Task.FromResult(new FileUploadWorkflowResult(FileUploadWorkflowStatus.MissingApiKey, 0)));
            using var closer = new WindowCloser(window);

            window.RunUploadAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.Equal("API key required before upload.", window.StatusText);
            Assert.All(window.QueueItemStatuses, status => Assert.Equal("Needs API key", status));
        });
    }

    [Fact]
    public void CompletionMarksEveryRowUploaded()
    {
        StaTestRunner.Run(() =>
        {
            var window = new UploadProgressWindow(
                Files(@"C:\a.txt"),
                _ => Task.FromResult(new FileUploadWorkflowResult(FileUploadWorkflowStatus.Completed, 1)));
            using var closer = new WindowCloser(window);

            window.RunUploadAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.Equal("Uploaded 1 item(s).", window.StatusText);
            Assert.Equal("Uploaded", window.FirstQueueItemStatus);
            Assert.Equal("1 of 1 uploaded", window.ViewModel.ProgressSummary);
            Assert.Equal("100%", window.ViewModel.ProgressPercent);
        });
    }

    [Fact]
    public void DuplicatePathsGetIndependentRowsMatchedPositionally()
    {
        StaTestRunner.Run(() =>
        {
            const string samePath = @"C:\same.txt";
            var window = new UploadProgressWindow(
                Files(samePath, samePath),
                _ => Task.FromResult(new FileUploadWorkflowResult(
                    FileUploadWorkflowStatus.Completed,
                    2,
                    ItemResults:
                    [
                        Item(samePath, "uploaded", "https://www.virustotal.com/gui/file/first"),
                        Item(samePath, "uploaded", "https://www.virustotal.com/gui/file/second"),
                    ])));
            using var closer = new WindowCloser(window);

            window.RunUploadAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.Equal(2, window.ViewModel.Items.Count);
            Assert.Equal("https://www.virustotal.com/gui/file/first", window.ViewModel.Items[0].ResultUrl);
            Assert.Equal("https://www.virustotal.com/gui/file/second", window.SecondQueueItemResultUrl);
        });
    }

    [Fact]
    public void TheFirstRowFlipsToUploadingTheMomentTheRunStarts()
    {
        StaTestRunner.Run(() =>
        {
            var gate = new TaskCompletionSource<FileUploadWorkflowResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var window = new UploadProgressWindow(Files(@"C:\a.txt", @"C:\b.txt"), _ => gate.Task);
            using var closer = new WindowCloser(window);

            var run = window.RunUploadAsync(CancellationToken.None);

            Assert.Equal("Uploading", window.FirstQueueItemStatus);
            Assert.True(window.ViewModel.IsUploadRunning);
            Assert.StartsWith("Uploading 0 of 2 item(s)", window.ViewModel.ProgressSummary, StringComparison.Ordinal);

            gate.SetResult(new FileUploadWorkflowResult(FileUploadWorkflowStatus.Completed, 2));
            run.GetAwaiter().GetResult();
            Assert.False(window.ViewModel.IsUploadRunning);
        });
    }

    [Fact]
    public void MixedItemResultsMapSuccessAndFailurePerRow()
    {
        StaTestRunner.Run(() =>
        {
            var window = new UploadProgressWindow(
                Files(@"C:\good.txt", @"C:\bad.txt"),
                _ => Task.FromResult(new FileUploadWorkflowResult(
                    FileUploadWorkflowStatus.CompletedWithFailures,
                    2,
                    FailedCount: 1,
                    ItemResults:
                    [
                        Item(@"C:\good.txt", "uploaded", "https://www.virustotal.com/gui/file/ok"),
                        Item(@"C:\bad.txt", "failed", error: "quota exceeded"),
                    ])));
            using var closer = new WindowCloser(window);

            window.RunUploadAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.Equal(["Uploaded", "Failed"], window.QueueItemStatuses);
            Assert.Equal("Failed: quota exceeded", window.ViewModel.Items[1].DisplayStatus);
            Assert.Equal("Uploaded 1 item(s), 1 failed.", window.StatusText);
            Assert.Equal("1 uploaded · 1 failed", window.ViewModel.ProgressSummary);
        });
    }

    [Fact]
    public void ACanceledRunKeepsCompletedRowsAndCancelsTheRest()
    {
        StaTestRunner.Run(() =>
        {
            var window = new UploadProgressWindow(
                Files(@"C:\done.txt", @"C:\pending.txt"),
                _ => Task.FromResult(new FileUploadWorkflowResult(
                    FileUploadWorkflowStatus.Canceled,
                    2,
                    ItemResults:
                    [
                        Item(@"C:\done.txt", "uploaded", "https://www.virustotal.com/gui/file/done"),
                    ])));
            using var closer = new WindowCloser(window);

            window.RunUploadAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.Equal(["Uploaded", "Canceled"], window.QueueItemStatuses);
            Assert.Equal("https://www.virustotal.com/gui/file/done", window.ViewModel.Items[0].ResultUrl);
            Assert.Null(window.ViewModel.Items[1].ResultUrl);
            Assert.Equal("Upload canceled after 1 of 2 item(s) uploaded.", window.StatusText);
            Assert.Equal("1 uploaded · 1 canceled", window.ViewModel.ProgressSummary);
        });
    }

    [Fact]
    public void PartialItemResultsForceResolveMissingRowsAsFailed()
    {
        StaTestRunner.Run(() =>
        {
            var window = new UploadProgressWindow(
                Files(@"C:\kept.txt", @"C:\vanished.txt"),
                _ => Task.FromResult(new FileUploadWorkflowResult(
                    FileUploadWorkflowStatus.CompletedWithFailures,
                    2,
                    FailedCount: 1,
                    ItemResults:
                    [
                        Item(@"C:\kept.txt", "uploaded", "https://www.virustotal.com/gui/file/kept"),
                    ])));
            using var closer = new WindowCloser(window);

            window.RunUploadAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.Equal(["Uploaded", "Failed"], window.QueueItemStatuses);
            Assert.Equal("File no longer exists.", window.ViewModel.Items[1].ErrorMessage);
        });
    }

    [Fact]
    public void RerunningAfterAFailureClearsStaleRowState()
    {
        StaTestRunner.Run(() =>
        {
            var attempts = 0;
            var window = new UploadProgressWindow(
                Files(@"C:\a.txt"),
                _ => ++attempts == 1
                    ? Task.FromResult(new FileUploadWorkflowResult(
                        FileUploadWorkflowStatus.Failed,
                        1,
                        FailedCount: 1,
                        ItemResults: [Item(@"C:\a.txt", "failed", error: "first try broke")]))
                    : Task.FromResult(new FileUploadWorkflowResult(
                        FileUploadWorkflowStatus.Completed,
                        1,
                        ItemResults: [Item(@"C:\a.txt", "uploaded", "https://www.virustotal.com/gui/file/ok")])));
            using var closer = new WindowCloser(window);

            window.RunUploadAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.Equal("Failed: first try broke", window.ViewModel.Items[0].DisplayStatus);

            window.RunUploadAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.Equal("Uploaded", window.FirstQueueItemStatus);
            Assert.Null(window.ViewModel.Items[0].ErrorMessage);
            Assert.Equal("Uploaded", window.ViewModel.Items[0].DisplayStatus);
        });
    }

    [Fact]
    public void AnActionExceptionMarksEveryQueuedRowFailed()
    {
        StaTestRunner.Run(() =>
        {
            var window = new UploadProgressWindow(
                Files(@"C:\a.txt", @"C:\b.txt"),
                _ => Task.FromException<FileUploadWorkflowResult>(new InvalidOperationException("socket died")));
            using var closer = new WindowCloser(window);

            window.RunUploadAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.Equal("Upload failed: socket died", window.StatusText);
            Assert.All(window.QueueItemStatuses, status => Assert.Equal("Failed", status));
            Assert.All(window.ViewModel.Items, item => Assert.Equal("socket died", item.ErrorMessage));
        });
    }

    [Fact]
    public void AnOperationCanceledExceptionCancelsPendingRows()
    {
        StaTestRunner.Run(() =>
        {
            var window = new UploadProgressWindow(
                Files(@"C:\a.txt"),
                _ => Task.FromException<FileUploadWorkflowResult>(new OperationCanceledException()));
            using var closer = new WindowCloser(window);

            window.RunUploadAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.Equal("Upload canceled.", window.StatusText);
            Assert.Equal("Canceled", window.FirstQueueItemStatus);
        });
    }

    [Fact]
    public void OpenResultIsGatedByTheUrlAllowlist()
    {
        StaTestRunner.Run(() =>
        {
            var launcher = new ExternalUrlLauncher(_ => { });
            var openedUrls = new List<string>();
            var gated = new UploadProgressWindow(
                Files(@"C:\a.txt"),
                _ => Task.FromResult(new FileUploadWorkflowResult(
                    FileUploadWorkflowStatus.Completed,
                    1,
                    ItemResults: [Item(@"C:\a.txt", "uploaded", "https://evil.example/phish")])),
                openResultUrl: openedUrls.Add,
                canOpenResultUrl: launcher.CanOpen);
            using var closerA = new WindowCloser(gated);

            gated.RunUploadAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.False(gated.ViewModel.OpenResultCommand.CanExecute(null));
            gated.OpenFirstResult();
            Assert.Empty(openedUrls);

            var noCallback = new UploadProgressWindow(
                Files(@"C:\a.txt"),
                _ => Task.FromResult(new FileUploadWorkflowResult(
                    FileUploadWorkflowStatus.Completed,
                    1,
                    ItemResults: [Item(@"C:\a.txt", "uploaded", "https://www.virustotal.com/gui/file/ok")])));
            using var closerB = new WindowCloser(noCallback);
            noCallback.RunUploadAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.False(noCallback.ViewModel.OpenResultCommand.CanExecute(null));
        });
    }

    [Fact]
    public void OpenResultOpensTheFirstRowWithAResultUrl()
    {
        StaTestRunner.Run(() =>
        {
            var openedUrls = new List<string>();
            var window = new UploadProgressWindow(
                Files(@"C:\failed.txt", @"C:\ok.txt"),
                _ => Task.FromResult(new FileUploadWorkflowResult(
                    FileUploadWorkflowStatus.CompletedWithFailures,
                    2,
                    FailedCount: 1,
                    ItemResults:
                    [
                        Item(@"C:\failed.txt", "failed", error: "nope"),
                        Item(@"C:\ok.txt", "uploaded", "https://www.virustotal.com/gui/file/ok"),
                    ])),
                openResultUrl: openedUrls.Add);
            using var closer = new WindowCloser(window);

            window.RunUploadAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.True(window.ViewModel.OpenResultCommand.CanExecute(null));
            window.OpenFirstResult();

            Assert.Equal(["https://www.virustotal.com/gui/file/ok"], openedUrls);
        });
    }

    [Fact]
    public void AutoStartReflectsTheConstructorAndFiresExactlyOnce()
    {
        StaTestRunner.Run(() =>
        {
            var runs = 0;
            var autoWindow = new UploadProgressWindow(
                Files(@"C:\a.txt"),
                _ => { runs++; return Task.FromResult(new FileUploadWorkflowResult(FileUploadWorkflowStatus.Completed, 1)); },
                autoStart: true);
            using var closerA = new WindowCloser(autoWindow);

            Assert.True(autoWindow.AutoStartsUpload);
            var first = autoWindow.TryTriggerAutoStart();
            Assert.NotNull(first);
            first!.GetAwaiter().GetResult();
            Assert.Null(autoWindow.TryTriggerAutoStart());
            Assert.Equal(1, runs);

            var manualWindow = new UploadProgressWindow(
                Files(@"C:\a.txt"),
                _ => Task.FromResult(new FileUploadWorkflowResult(FileUploadWorkflowStatus.Completed, 1)));
            using var closerB = new WindowCloser(manualWindow);

            Assert.False(manualWindow.AutoStartsUpload);
            Assert.Null(manualWindow.TryTriggerAutoStart());
            Assert.Equal("Waiting to upload", manualWindow.StatusText);
        });
    }

    [Fact]
    public void UploadCommandIsUnavailableWhileRunningOrWithoutAnAction()
    {
        StaTestRunner.Run(() =>
        {
            var actionless = new UploadProgressWindow(Files(@"C:\a.txt"));
            using var closerA = new WindowCloser(actionless);
            Assert.False(actionless.ViewModel.UploadCommand.CanExecute(null));

            var gate = new TaskCompletionSource<FileUploadWorkflowResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var window = new UploadProgressWindow(Files(@"C:\a.txt"), _ => gate.Task);
            using var closerB = new WindowCloser(window);

            Assert.True(window.ViewModel.UploadCommand.CanExecute(null));
            var run = window.RunUploadAsync(CancellationToken.None);
            Assert.False(window.ViewModel.UploadCommand.CanExecute(null));

            gate.SetResult(new FileUploadWorkflowResult(FileUploadWorkflowStatus.Completed, 1));
            run.GetAwaiter().GetResult();
            Assert.True(window.ViewModel.UploadCommand.CanExecute(null));
        });
    }

    [Fact]
    public void CancelIsCooperativeNotImmediate()
    {
        StaTestRunner.Run(() =>
        {
            var actionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var tokenObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseAction = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var window = new UploadProgressWindow(
                Files(@"C:\a.txt"),
                async token =>
                {
                    token.Register(() => tokenObserved.TrySetResult());
                    actionStarted.SetResult();
                    await releaseAction.Task;
                    token.ThrowIfCancellationRequested();
                    return new FileUploadWorkflowResult(FileUploadWorkflowStatus.Completed, 1);
                });
            using var closer = new WindowCloser(window);

            var run = window.ViewModel.StartManagedUploadAsync(CancellationToken.None);
            actionStarted.Task.GetAwaiter().GetResult();

            // Phase 1: cancel is requested but the action has not returned yet.
            window.ViewModel.CancelCommand.Execute(null);
            Assert.True(window.ViewModel.IsUploadRunning);
            Assert.Equal("Canceling upload...", window.StatusText);
            Assert.Equal("Cancel", window.ViewModel.CancelButtonText);

            // Phase 2: the running action's own token observed the request.
            Assert.True(tokenObserved.Task.Wait(TimeSpan.FromSeconds(5)));

            // Phase 3: only after the action returns does the run resolve.
            releaseAction.SetResult();
            run.GetAwaiter().GetResult();

            Assert.False(window.ViewModel.IsUploadRunning);
            Assert.Equal("Upload canceled.", window.StatusText);
            Assert.Equal("Canceled", window.FirstQueueItemStatus);
            Assert.Equal("Close", window.ViewModel.CancelButtonText);
        });
    }

    [Fact]
    public void CancelClosesTheWindowOnceTheRunHasFinished()
    {
        StaTestRunner.Run(() =>
        {
            var window = new UploadProgressWindow(
                Files(@"C:\a.txt"),
                _ => Task.FromResult(new FileUploadWorkflowResult(FileUploadWorkflowStatus.Completed, 1)));

            window.RunUploadAsync(CancellationToken.None).GetAwaiter().GetResult();
            window.ViewModel.CancelCommand.Execute(null);

            Assert.False(window.IsVisible);
        });
    }

    [Fact]
    public void ClosingTheWindowCancelsAnInFlightManagedUpload()
    {
        StaTestRunner.Run(() =>
        {
            var actionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var tokenCanceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var window = new UploadProgressWindow(
                Files(@"C:\a.txt"),
                token =>
                {
                    token.Register(() => tokenCanceled.TrySetResult());
                    actionStarted.SetResult();
                    return new TaskCompletionSource<FileUploadWorkflowResult>().Task;
                });

            _ = window.ViewModel.StartManagedUploadAsync(CancellationToken.None);
            actionStarted.Task.GetAwaiter().GetResult();
            window.Close();

            Assert.True(tokenCanceled.Task.Wait(TimeSpan.FromSeconds(5)));
        });
    }

    [Fact]
    public void TheWindowShowsTheQueueHeaderAndApiInfoBar()
    {
        StaTestRunner.Run(() =>
        {
            var window = new UploadProgressWindow(Files(@"C:\a.txt"));
            using var closer = new WindowCloser(window);

            VisualTreeAssert.AssertWindowContainsText(window, "Upload queue");
            VisualTreeAssert.AssertWindowContainsText(window, "Files upload one at a time using VirusTotal API v3.");
            VisualTreeAssert.AssertWindowContainsText(window, @"C:\a.txt");
        });
    }
}
