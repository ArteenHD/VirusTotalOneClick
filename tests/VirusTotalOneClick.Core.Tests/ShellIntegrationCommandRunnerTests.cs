namespace VirusTotalOneClick.Core.Tests;

public class ShellIntegrationCommandRunnerTests
{
    private static readonly ShellIntegrationOptions Options = new(@"C:\Apps\VirusTotalOneClick.exe", "Upload to VirusTotal");

    private sealed class RecordingApplier : IShellIntegrationApplier
    {
        public List<AppSettings> Applied { get; } = [];

        public void Apply(AppSettings settings, ShellIntegrationOptions options) => Applied.Add(settings);
    }

    [Fact]
    public void AppModeHasExactlyTheSixExpectedValues()
    {
        Assert.Equal(
            new[] { "Interactive", "Settings", "History", "Upload", "InstallShell", "UninstallShell" },
            Enum.GetNames<AppMode>());
    }

    [Fact]
    public void InstallShellWithNoSavedSettingsAppliesTheDefaults()
    {
        var applier = new RecordingApplier();
        var runner = new ShellIntegrationCommandRunner(applier, Options);

        Assert.True(runner.TryRun(AppMode.InstallShell));

        var applied = Assert.Single(applier.Applied);
        Assert.Equal(AppSettings.Default, applied);
    }

    [Fact]
    public void InstallShellRespectsPreviouslySavedSettings()
    {
        var saved = AppSettings.Default with { EnableSendToShortcut = false, LaunchOnStartup = true };
        var applier = new RecordingApplier();
        var runner = new ShellIntegrationCommandRunner(applier, Options, () => saved);

        Assert.True(runner.TryRun(AppMode.InstallShell));

        Assert.Equal(saved, Assert.Single(applier.Applied));
    }

    [Fact]
    public void UninstallShellForcesEverySurfaceOffRegardlessOfSavedSettings()
    {
        var saved = AppSettings.Default with { LaunchOnStartup = true };
        var applier = new RecordingApplier();
        var runner = new ShellIntegrationCommandRunner(applier, Options, () => saved);

        Assert.True(runner.TryRun(AppMode.UninstallShell));

        var applied = Assert.Single(applier.Applied);
        Assert.False(applied.EnableClassicContextMenu);
        Assert.False(applied.EnableSendToShortcut);
        Assert.False(applied.EnableStartMenuShortcuts);
        Assert.False(applied.LaunchOnStartup);
    }

    [Theory]
    [InlineData(AppMode.Interactive)]
    [InlineData(AppMode.Settings)]
    [InlineData(AppMode.History)]
    [InlineData(AppMode.Upload)]
    public void OtherModesAreNotHandled(AppMode mode)
    {
        var applier = new RecordingApplier();
        var runner = new ShellIntegrationCommandRunner(applier, Options);

        Assert.False(runner.TryRun(mode));
        Assert.Empty(applier.Applied);
    }
}
