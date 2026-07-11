namespace VirusTotalOneClick.Core.Tests;

public class ReviewRootLocatorTests
{
    [Fact]
    public void AnEmptySelectionYieldsAnEmptyRoot()
    {
        Assert.Equal(string.Empty, ReviewRootLocator.FindCommonRoot([]));
    }

    [Fact]
    public void ASingleFileUsesItsParentDirectory()
    {
        var root = ReviewRootLocator.FindCommonRoot(
            [new FileSelection(@"C:\projects\demo\readme.txt", IsDirectory: false)]);

        Assert.Equal(@"C:\projects\demo", root);
    }

    [Fact]
    public void ASingleDirectoryUsesItself()
    {
        var root = ReviewRootLocator.FindCommonRoot(
            [new FileSelection(@"C:\projects\demo", IsDirectory: true)]);

        Assert.Equal(@"C:\projects\demo", root);
    }

    [Fact]
    public void SiblingSelectionsShareTheirParent()
    {
        var root = ReviewRootLocator.FindCommonRoot(
        [
            new FileSelection(@"C:\projects\demo\src", IsDirectory: true),
            new FileSelection(@"C:\projects\demo\readme.txt", IsDirectory: false),
        ]);

        Assert.Equal(@"C:\projects\demo", root);
    }

    [Fact]
    public void ANestedSelectionCollapsesToTheShallowerDirectory()
    {
        var root = ReviewRootLocator.FindCommonRoot(
        [
            new FileSelection(@"C:\projects\demo", IsDirectory: true),
            new FileSelection(@"C:\projects\demo\src\inner", IsDirectory: true),
        ]);

        Assert.Equal(@"C:\projects\demo", root);
    }

    [Fact]
    public void PathComparisonIsCaseInsensitive()
    {
        var root = ReviewRootLocator.FindCommonRoot(
        [
            new FileSelection(@"C:\Projects\Demo\a.txt", IsDirectory: false),
            new FileSelection(@"c:\projects\demo\b.txt", IsDirectory: false),
        ]);

        Assert.Equal(@"C:\Projects\Demo", root, ignoreCase: true);
    }

    [Fact]
    public void SelectionsSharingOnlyTheDriveFallBackToTheDriveRoot()
    {
        var root = ReviewRootLocator.FindCommonRoot(
        [
            new FileSelection(@"C:\alpha\a.txt", IsDirectory: false),
            new FileSelection(@"C:\beta\b.txt", IsDirectory: false),
        ]);

        Assert.Equal(@"C:\", root);
    }

    [Fact]
    public void SelectionsOnDifferentDrivesFallBackToTheFirstDriveRoot()
    {
        var root = ReviewRootLocator.FindCommonRoot(
        [
            new FileSelection(@"C:\alpha\a.txt", IsDirectory: false),
            new FileSelection(@"D:\beta\b.txt", IsDirectory: false),
        ]);

        Assert.Equal(@"C:\", root);
    }
}
