using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using VirusTotalOneClick.Core;
using VirusTotalOneClick.UI;

namespace VirusTotalOneClick.AppTests;

public class GridViewColumnFillTests
{
    [Fact]
    public void WeightedColumnsShareTheLeftoverSpaceProportionally()
    {
        var widths = GridViewColumnFill.Distribute(
            viewportWidth: 900 + GridViewColumnFill.FallbackRowChrome + GridViewColumnFill.RoundingPad,
            rowChrome: GridViewColumnFill.FallbackRowChrome,
            fixedWidths: [100, 220, 150],
            weights: [0, 3, 2]);

        Assert.Equal(100, widths[0]);
        Assert.Equal(480, widths[1]);
        Assert.Equal(320, widths[2]);
        Assert.Equal(900, widths.Sum());
    }

    [Fact]
    public void ACrampedViewportFloorsFlexibleColumnsInsteadOfVanishingThem()
    {
        var widths = GridViewColumnFill.Distribute(
            viewportWidth: 300,
            rowChrome: GridViewColumnFill.FallbackRowChrome,
            fixedWidths: [250, 200, 180],
            weights: [0, 1, 1]);

        Assert.Equal(250, widths[0]);
        Assert.Equal(GridViewColumnFill.MinimumFlexibleWidth, widths[1]);
        Assert.Equal(GridViewColumnFill.MinimumFlexibleWidth, widths[2]);
    }

    [Fact]
    public void TheHistoryTableFitsTheDefaultWindowWithoutAHorizontalScrollbar()
    {
        StaTestRunner.Run(() =>
        {
            var entries = Enumerable.Range(0, 20)
                .Select(i => new UploadHistoryEntry(
                    $@"C:\Users\Sandbox\Desktop\some-long-folder-name\sample-folder\file-number-{i:d2}.txt",
                    $"file-number-{i:d2}.txt",
                    new string('a', 64),
                    DateTimeOffset.UtcNow.AddMinutes(-i),
                    "file",
                    "uploaded",
                    "https://www.virustotal.com/gui/file/" + new string('a', 64)))
                .ToList();
            var window = new HistoryWindow(entries);
            using var closer = new WindowCloser(window);

            window.Show();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

            var scrollViewer = VisualTreeAssert.FindVisualChild<ScrollViewer>(
                VisualTreeAssert.FindVisualChild<ListView>(window)!);
            Assert.NotNull(scrollViewer);
            Assert.Equal(0, scrollViewer.ScrollableWidth);
        });
    }

    [Fact]
    public void TheUploadQueueTableFitsTheDefaultWindowWithoutAHorizontalScrollbar()
    {
        StaTestRunner.Run(() =>
        {
            var files = Enumerable.Range(0, 10)
                .Select(i => new FileSelection($@"C:\Users\Sandbox\Desktop\sample-folder\file-number-{i:d2}.txt", IsDirectory: false))
                .ToList();
            var window = new UploadProgressWindow(
                files,
                _ => Task.FromResult(new FileUploadWorkflowResult(FileUploadWorkflowStatus.Completed, files.Count)));
            using var closer = new WindowCloser(window);

            window.Show();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

            var scrollViewer = VisualTreeAssert.FindVisualChild<ScrollViewer>(
                VisualTreeAssert.FindVisualChild<ListView>(window)!);
            Assert.NotNull(scrollViewer);
            Assert.Equal(0, scrollViewer.ScrollableWidth);
        });
    }

    [Fact]
    public void AColumnTheUserResizedByItsGripperKeepsTheUsersWidth()
    {
        StaTestRunner.Run(() =>
        {
            var window = new HistoryWindow(
            [
                new UploadHistoryEntry(
                    @"C:\a.txt", "a.txt", new string('c', 64), DateTimeOffset.UtcNow, "file", "uploaded",
                    "https://www.virustotal.com/gui/file/" + new string('c', 64)),
            ]);
            using var closer = new WindowCloser(window);

            window.Show();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

            var listView = VisualTreeAssert.FindVisualChild<ListView>(window)!;
            var gridView = (GridView)listView.View;
            var originalFileColumn = gridView.Columns[1];

            // A drag ends with the column at the user's width and the header
            // gripper raising DragCompleted, which bubbles up to the ListView.
            originalFileColumn.Width = 320;
            var header = VisualTreeAssert.FindVisualChildren<GridViewColumnHeader>(listView)
                .First(candidate => candidate.Column == originalFileColumn);
            var gripper = VisualTreeAssert.FindVisualChild<Thumb>(header)!;
            gripper.RaiseEvent(new DragCompletedEventArgs(0, 0, canceled: false)
            {
                RoutedEvent = Thumb.DragCompletedEvent,
            });

            window.Width = AppWindow.TaskWindowWidth + 150;
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

            Assert.Equal(320, originalFileColumn.Width);
        });
    }

    [Fact]
    public void DraggingAScrollbarThumbDoesNotPinAnyColumn()
    {
        StaTestRunner.Run(() =>
        {
            var entries = Enumerable.Range(0, 40)
                .Select(i => new UploadHistoryEntry(
                    $@"C:\file-{i:d2}.txt", $"file-{i:d2}.txt", new string('d', 64),
                    DateTimeOffset.UtcNow.AddMinutes(-i), "file", "uploaded",
                    "https://www.virustotal.com/gui/file/" + new string('d', 64)))
                .ToList();
            var window = new HistoryWindow(entries);
            using var closer = new WindowCloser(window);

            window.Show();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

            var listView = VisualTreeAssert.FindVisualChild<ListView>(window)!;
            var gridView = (GridView)listView.View;
            var originalFileColumn = gridView.Columns[1];
            var widthAtDefault = originalFileColumn.Width;

            var scrollBarThumb = VisualTreeAssert.FindVisualChildren<ScrollBar>(listView)
                .Select(VisualTreeAssert.FindVisualChild<Thumb>)
                .First(thumb => thumb is not null)!;
            scrollBarThumb.RaiseEvent(new DragCompletedEventArgs(0, 0, canceled: false)
            {
                RoutedEvent = Thumb.DragCompletedEvent,
            });

            window.Width = AppWindow.TaskWindowWidth + 150;
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

            Assert.True(
                originalFileColumn.Width > widthAtDefault,
                $"expected the column to keep filling ({widthAtDefault} -> wider), got {originalFileColumn.Width}");
        });
    }

    [Fact]
    public void WideningTheWindowGrowsTheFlexibleColumnsAndKeepsTheFit()
    {
        StaTestRunner.Run(() =>
        {
            var window = new HistoryWindow(
            [
                new UploadHistoryEntry(
                    @"C:\a.txt", "a.txt", new string('b', 64), DateTimeOffset.UtcNow, "file", "uploaded",
                    "https://www.virustotal.com/gui/file/" + new string('b', 64)),
            ]);
            using var closer = new WindowCloser(window);

            window.Show();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

            var listView = VisualTreeAssert.FindVisualChild<ListView>(window)!;
            var gridView = (GridView)listView.View;
            var originalFileWidthAtDefault = gridView.Columns[1].Width;

            window.Width = AppWindow.TaskWindowWidth + 200;
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

            var scrollViewer = VisualTreeAssert.FindVisualChild<ScrollViewer>(listView)!;
            Assert.Equal(0, scrollViewer.ScrollableWidth);
            Assert.True(
                gridView.Columns[1].Width > originalFileWidthAtDefault,
                $"expected the Original file column to grow past {originalFileWidthAtDefault}, got {gridView.Columns[1].Width}");
        });
    }
}
