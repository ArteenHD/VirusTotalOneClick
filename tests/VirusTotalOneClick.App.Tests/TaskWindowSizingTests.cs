using System.Windows;
using VirusTotalOneClick.Core;
using VirusTotalOneClick.UI;

namespace VirusTotalOneClick.AppTests;

public class TaskWindowSizingTests
{
    [Fact]
    public void TaskWindowsShareOneDefaultAndMinimumSize()
    {
        StaTestRunner.Run(() =>
        {
            var queue = new UploadProgressWindow(
                [new FileSelection(@"C:\a.txt", IsDirectory: false)],
                _ => Task.FromResult(new FileUploadWorkflowResult(FileUploadWorkflowStatus.Completed, 1)));
            using var closerA = new WindowCloser(queue);

            var review = new FolderReviewWindow(
                @"C:\root",
                _ => Task.FromResult(new FolderScanResult([], Truncated: false, Errors: [])));
            using var closerB = new WindowCloser(review);

            var history = new HistoryWindow([]);
            using var closerC = new WindowCloser(history);

            // Height goes through the work-area clamp so a short display
            // never opens a footer below the taskbar; on a roomy display the
            // clamp returns the design height unchanged.
            var expectedHeight = AppWindow.ClampHeightToWorkArea(
                AppWindow.TaskWindowHeight, SystemParameters.WorkArea.Height, AppWindow.TaskWindowMinHeight);
            foreach (Window window in (Window[])[queue, review, history])
            {
                Assert.Equal(AppWindow.TaskWindowWidth, window.Width);
                Assert.Equal(expectedHeight, window.Height);
                Assert.Equal(AppWindow.TaskWindowMinWidth, window.MinWidth);
                Assert.Equal(AppWindow.TaskWindowMinHeight, window.MinHeight);
            }
        });
    }

    [Fact]
    public void TheDefaultSizeStaysWithinTheMinimums()
    {
        Assert.True(AppWindow.TaskWindowWidth >= AppWindow.TaskWindowMinWidth);
        Assert.True(AppWindow.TaskWindowHeight >= AppWindow.TaskWindowMinHeight);
    }
}
