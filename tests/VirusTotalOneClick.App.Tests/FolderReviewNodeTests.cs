using VirusTotalOneClick.UI;

namespace VirusTotalOneClick.AppTests;

public class FolderReviewNodeTests
{
    private static (FolderReviewNode Root, FolderReviewNode Dir, FolderReviewNode FileA, FolderReviewNode FileB, FolderReviewNode Loose) BuildTree()
    {
        var root = new FolderReviewNode("root", @"C:\root", isDirectory: true);
        var dir = new FolderReviewNode("sub", @"C:\root\sub", isDirectory: true) { Parent = root };
        root.Children.Add(dir);
        var fileA = new FolderReviewNode("a.txt", @"C:\root\sub\a.txt", isDirectory: false, 10) { Parent = dir };
        var fileB = new FolderReviewNode("b.txt", @"C:\root\sub\b.txt", isDirectory: false, 20) { Parent = dir };
        dir.Children.Add(fileA);
        dir.Children.Add(fileB);
        var loose = new FolderReviewNode("loose.txt", @"C:\root\loose.txt", isDirectory: false, 5) { Parent = root };
        root.Children.Add(loose);
        return (root, dir, fileA, fileB, loose);
    }

    [Fact]
    public void CheckingAFolderCascadesToEveryDescendant()
    {
        var (root, dir, fileA, fileB, loose) = BuildTree();

        root.IsChecked = false;

        Assert.False(dir.IsChecked);
        Assert.False(fileA.IsChecked);
        Assert.False(fileB.IsChecked);
        Assert.False(loose.IsChecked);
    }

    [Fact]
    public void AMixedSubtreeShowsTheIndeterminateState()
    {
        var (root, dir, fileA, _, _) = BuildTree();

        fileA.IsChecked = false;

        Assert.Null(dir.IsChecked);
        Assert.Null(root.IsChecked);
    }

    [Fact]
    public void ReCheckingEveryLeafRestoresTheParentCheckedState()
    {
        var (root, dir, fileA, _, _) = BuildTree();
        fileA.IsChecked = false;

        fileA.IsChecked = true;

        Assert.True(dir.IsChecked);
        Assert.True(root.IsChecked);
    }

    [Fact]
    public void UncheckingASubtreeExcludesAllItsLeavesFromTheSelection()
    {
        var (root, dir, _, _, loose) = BuildTree();

        dir.IsChecked = false;

        var selected = root.SelectedFiles().Select(node => node.FullPath).ToList();
        Assert.Equal([loose.FullPath], selected);
    }

    [Fact]
    public void TheCascadeCompletesExactlyOnceOnTheInitiatingNode()
    {
        var (root, _, _, _, _) = BuildTree();
        var completions = 0;
        root.CheckedCascadeCompleted += (_, _) => completions++;

        root.IsChecked = false;

        Assert.Equal(1, completions);
        Assert.False(FolderReviewNode.IsCascadeInProgress);
    }

    [Fact]
    public void CheckedStateChangesDuringACascadeAreObservableAsInProgress()
    {
        var (root, _, fileA, _, _) = BuildTree();
        bool? inProgressDuringChildChange = null;
        fileA.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FolderReviewNode.IsChecked))
            {
                inProgressDuringChildChange = FolderReviewNode.IsCascadeInProgress;
            }
        };

        root.IsChecked = false;

        Assert.True(inProgressDuringChildChange);
    }

    [Fact]
    public void DirectoryDetailCountsFilesAndFileDetailShowsSize()
    {
        var (root, dir, fileA, _, _) = BuildTree();

        Assert.Equal("3 files", root.DisplayDetail);
        Assert.Equal("2 files", dir.DisplayDetail);
        Assert.Equal("10 B", fileA.DisplayDetail);
    }
}
