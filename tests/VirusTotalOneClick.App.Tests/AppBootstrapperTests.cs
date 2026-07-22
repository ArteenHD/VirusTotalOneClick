using System.Net.Http;
using System.Reflection;
using VirusTotalOneClick.Core;

namespace VirusTotalOneClick.AppTests;

public class AppBootstrapperTests
{
    private static readonly Uri ApiBase = new("https://www.virustotal.com/api/v3/");

    [Theory]
    [InlineData(AppMode.Interactive)]
    [InlineData(AppMode.Settings)]
    [InlineData(AppMode.History)]
    [InlineData(AppMode.Upload)]
    public void WindowModesAreNotHandledNonInteractively(AppMode mode)
    {
        using var root = new TemporaryFolder();

        var handled = AppBootstrapper.TryRunNonInteractive(
            new CommandLineOptions(mode, []),
            AppPaths.FromRoot(root.Path));

        Assert.False(handled);
    }

    [Fact]
    public void FirstRunInteractiveLaunchStaysInteractive()
    {
        using var root = new TemporaryFolder();

        var composition = AppBootstrapper.Compose(
            new CommandLineOptions(AppMode.Interactive, []),
            AppPaths.FromRoot(root.Path));

        Assert.Equal(AppMode.Interactive, composition.Options.Mode);
    }

    [Fact]
    public void InteractiveLaunchWithSavedSettingsResolvesToSettings()
    {
        using var root = new TemporaryFolder();
        var paths = AppPaths.FromRoot(root.Path);
        new AppSettingsStore(paths.SettingsPath).Save(AppSettings.Default);

        var composition = AppBootstrapper.Compose(
            new CommandLineOptions(AppMode.Interactive, []), paths);

        Assert.Equal(AppMode.Settings, composition.Options.Mode);
        Assert.Empty(composition.Options.Paths);
    }

    [Fact]
    public void ExplicitModesAreNeverRewritten()
    {
        using var root = new TemporaryFolder();
        var paths = AppPaths.FromRoot(root.Path);
        new AppSettingsStore(paths.SettingsPath).Save(AppSettings.Default);

        var composition = AppBootstrapper.Compose(
            new CommandLineOptions(AppMode.Upload, ["a.txt"]), paths);

        Assert.Equal(AppMode.Upload, composition.Options.Mode);
        Assert.Equal(["a.txt"], composition.Options.Paths);
    }

    [Fact]
    public void ComposedSettingsComeFromTheSettingsFile()
    {
        using var root = new TemporaryFolder();
        var paths = AppPaths.FromRoot(root.Path);
        new AppSettingsStore(paths.SettingsPath)
            .Save(AppSettings.Default with { ScrubSubmittedFilename = true });

        var composition = AppBootstrapper.Compose(
            new CommandLineOptions(AppMode.Interactive, []), paths);

        Assert.True(composition.Settings.ScrubSubmittedFilename);
    }

    [Fact]
    public async Task TheComposedUploadActionRunsTheRealWorkflow()
    {
        using var root = new TemporaryFolder();
        var file = root.CreateFile("sample.txt", "content");

        var composition = AppBootstrapper.Compose(
            new CommandLineOptions(AppMode.Upload, [file]),
            AppPaths.FromRoot(root.Path));
        var result = await composition.CreateUploadAction([file])(CancellationToken.None);

        Assert.Equal(FileUploadWorkflowStatus.MissingApiKey, result.Status);
    }

    [Fact]
    public async Task TheComposedZipActionDrivesTheFolderZipWorkflow()
    {
        using var root = new TemporaryFolder();
        var source = root.CreateSubdirectory("target");
        var file = root.CreateFile(@"target\inner.txt", "content");

        var composition = AppBootstrapper.Compose(
            new CommandLineOptions(AppMode.Upload, [source]),
            AppPaths.FromRoot(root.Path));
        var result = await composition
            .CreateZipUploadAction(new FolderZipUploadRequest(source, [file]))(CancellationToken.None);

        Assert.Equal(FileUploadWorkflowStatus.MissingApiKey, result.Status);
    }

    [Fact]
    public void HistorySeamsRoundTripThroughTheComposedStore()
    {
        using var root = new TemporaryFolder();
        var composition = AppBootstrapper.Compose(
            new CommandLineOptions(AppMode.History, []),
            AppPaths.FromRoot(root.Path));

        Assert.Empty(composition.ReadHistory());
        composition.ClearHistory();

        Assert.Empty(composition.ReadHistory());
    }

    [Fact]
    public void TheUploadClientNeverTimesOutOnItsOwn()
    {
        Assert.Equal(Timeout.InfiniteTimeSpan, AppBootstrapper.UploadHttpClient.Timeout);
        Assert.Equal(ApiBase, AppBootstrapper.UploadHttpClient.BaseAddress);
    }

    [Fact]
    public void TheUploadClientNeverFollowsRedirects()
    {
        Assert.False(AppBootstrapper.UploadHttpHandler.AllowAutoRedirect);
        Assert.Same(AppBootstrapper.UploadHttpHandler, GetWiredHandler(AppBootstrapper.UploadHttpClient));
    }

    private static HttpMessageHandler GetWiredHandler(HttpClient client)
    {
        var field = typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (HttpMessageHandler)field.GetValue(client)!;
    }

    [Fact]
    public void TheValidationClientUsesAShortTimeout()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), AppBootstrapper.ValidationHttpClient.Timeout);
        Assert.Equal(ApiBase, AppBootstrapper.ValidationHttpClient.BaseAddress);
    }

    [Fact]
    public void TheExecutablePathIsAlwaysResolvable()
    {
        var path = AppBootstrapper.GetExecutablePath();

        Assert.False(string.IsNullOrWhiteSpace(path));
        Assert.True(Path.IsPathRooted(path));
    }
}
