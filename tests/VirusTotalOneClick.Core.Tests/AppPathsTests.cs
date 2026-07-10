namespace VirusTotalOneClick.Core.Tests;

public class AppPathsTests
{
    [Fact]
    public void FromRootDerivesDirectoryAndFilePaths()
    {
        var paths = AppPaths.FromRoot(@"C:\Users\someone\AppData\Roaming");

        Assert.Equal(@"C:\Users\someone\AppData\Roaming\VirusTotalOneClick", paths.AppDataDirectory);
        Assert.Equal(@"C:\Users\someone\AppData\Roaming\VirusTotalOneClick\settings.json", paths.SettingsPath);
        Assert.Equal(@"C:\Users\someone\AppData\Roaming\VirusTotalOneClick\history.jsonl", paths.HistoryPath);
    }

    [Fact]
    public void FromCurrentUserRootsUnderRoamingAppData()
    {
        var paths = AppPaths.FromCurrentUser();
        var expectedRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        Assert.StartsWith(expectedRoot, paths.AppDataDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(@"\VirusTotalOneClick", paths.AppDataDirectory, StringComparison.Ordinal);
    }
}
