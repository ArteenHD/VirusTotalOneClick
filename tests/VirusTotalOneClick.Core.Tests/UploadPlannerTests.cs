namespace VirusTotalOneClick.Core.Tests;

public class UploadPlannerTests
{
    [Fact]
    public void SingleFileUploadsImmediatelyWhenConfirmationIsOff()
    {
        var plan = UploadPlanner.Plan([new FileSelection(@"C:\samples\a.exe", IsDirectory: false)], confirmFiles: false);

        Assert.True(plan.CanUploadImmediately);
        Assert.False(plan.RequiresFolderReview);
    }

    [Fact]
    public void SingleFileWaitsWhenConfirmationIsOn()
    {
        var plan = UploadPlanner.Plan([new FileSelection(@"C:\samples\a.exe", IsDirectory: false)], confirmFiles: true);

        Assert.False(plan.CanUploadImmediately);
        Assert.False(plan.RequiresFolderReview);
    }

    [Fact]
    public void MultipleLooseFilesUploadImmediatelyWhenConfirmationIsOff()
    {
        var plan = UploadPlanner.Plan(
            [
                new FileSelection(@"C:\samples\a.exe", IsDirectory: false),
                new FileSelection(@"C:\samples\b.dll", IsDirectory: false),
            ],
            confirmFiles: false);

        Assert.True(plan.CanUploadImmediately);
        Assert.False(plan.RequiresFolderReview);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnyFolderAlwaysRoutesToReview(bool confirmFiles)
    {
        var plan = UploadPlanner.Plan([new FileSelection(@"C:\samples", IsDirectory: true)], confirmFiles);

        Assert.True(plan.RequiresFolderReview);
        Assert.False(plan.CanUploadImmediately);
    }

    [Fact]
    public void MixedFilesAndFoldersRouteTheWholeSelectionToReview()
    {
        var selections = new[]
        {
            new FileSelection(@"C:\samples\a.exe", IsDirectory: false),
            new FileSelection(@"C:\samples\folder", IsDirectory: true),
        };

        var plan = UploadPlanner.Plan(selections, confirmFiles: false);

        Assert.True(plan.RequiresFolderReview);
        Assert.False(plan.CanUploadImmediately);
        Assert.Equal(selections, plan.Selections);
    }

    [Fact]
    public void EmptySelectionNeitherUploadsNorReviews()
    {
        var plan = UploadPlanner.Plan([], confirmFiles: false);

        Assert.False(plan.CanUploadImmediately);
        Assert.False(plan.RequiresFolderReview);
    }
}
