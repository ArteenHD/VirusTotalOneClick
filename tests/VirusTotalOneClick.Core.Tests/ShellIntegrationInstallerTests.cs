namespace VirusTotalOneClick.Core.Tests;

public class ShellIntegrationInstallerTests
{
    private static readonly ShellIntegrationOptions Options = new(@"C:\Apps\VirusTotalOneClick.exe", "Upload to VirusTotal");

    [Fact]
    public void FullyEnabledSettingsWriteEverySurface()
    {
        var registry = new RecordingRegistryWriter();
        var sendTo = new RecordingSendToShortcutWriter();
        var startMenu = new RecordingStartMenuShortcutWriter();
        var startup = new RecordingStartupShortcutWriter();
        var installer = new ShellIntegrationInstaller(registry, sendTo, startMenu, startup);
        var settings = AppSettings.Default with { LaunchOnStartup = true };

        installer.Apply(settings, Options);

        Assert.Equal(8, registry.SetValues.Count);
        Assert.Contains(registry.SetValues, entry => entry.KeyPath.EndsWith(@"\command", StringComparison.Ordinal)
            && entry.Value == "\"C:\\Apps\\VirusTotalOneClick.exe\" upload --path \"%1\"");
        var sendToRequest = Assert.Single(sendTo.Created);
        Assert.Equal("upload", sendToRequest.Arguments);
        var startMenuRequest = Assert.Single(startMenu.Created);
        Assert.Equal(string.Empty, startMenuRequest.Arguments);
        var startupRequest = Assert.Single(startup.Created);
        Assert.Equal("settings", startupRequest.Arguments);
    }

    [Fact]
    public void DefaultSettingsLeaveStartupDisabled()
    {
        var startup = new RecordingStartupShortcutWriter();
        var installer = new ShellIntegrationInstaller(
            new RecordingRegistryWriter(), new RecordingSendToShortcutWriter(), new RecordingStartMenuShortcutWriter(), startup);

        installer.Apply(AppSettings.Default, Options);

        Assert.Empty(startup.Created);
        Assert.Equal(1, startup.DeleteCount);
    }

    [Fact]
    public void ARegistryFailureDoesNotStopTheOtherStepsAndAllFailuresAggregate()
    {
        var sendTo = new RecordingSendToShortcutWriter();
        var startMenu = new RecordingStartMenuShortcutWriter();
        var startup = new RecordingStartupShortcutWriter();
        var installer = new ShellIntegrationInstaller(new ThrowingRegistryWriter(), sendTo, startMenu, startup);

        var exception = Assert.Throws<AggregateException>(() => installer.Apply(AppSettings.Default, Options));

        Assert.Single(exception.InnerExceptions);
        Assert.IsType<UnauthorizedAccessException>(exception.InnerExceptions[0]);
        Assert.StartsWith("One or more shell integration steps failed.", exception.Message, StringComparison.Ordinal);
        Assert.Single(sendTo.Created);
        Assert.Single(startMenu.Created);
        Assert.Equal(1, startup.DeleteCount);
    }

    [Fact]
    public void AFailingTearDownStepStillTearsDownTheOtherSurfaces()
    {
        var sendTo = new RecordingSendToShortcutWriter();
        var startMenu = new RecordingStartMenuShortcutWriter();
        var startup = new RecordingStartupShortcutWriter();
        var installer = new ShellIntegrationInstaller(new ThrowingRegistryWriter(), sendTo, startMenu, startup);
        var allOff = AppSettings.Default with
        {
            EnableClassicContextMenu = false,
            EnableSendToShortcut = false,
            EnableStartMenuShortcuts = false,
            LaunchOnStartup = false,
        };

        var exception = Assert.Throws<AggregateException>(() => installer.Apply(allOff, Options));

        Assert.Single(exception.InnerExceptions);
        Assert.Equal(1, sendTo.DeleteCount);
        Assert.Equal(1, startMenu.DeleteAllCount);
        Assert.Equal(1, startup.DeleteCount);
    }

    [Fact]
    public void DisabledSettingsTearDownAllFourSurfaces()
    {
        var registry = new RecordingRegistryWriter();
        var sendTo = new RecordingSendToShortcutWriter();
        var startMenu = new RecordingStartMenuShortcutWriter();
        var startup = new RecordingStartupShortcutWriter();
        var installer = new ShellIntegrationInstaller(registry, sendTo, startMenu, startup);
        var allOff = AppSettings.Default with
        {
            EnableClassicContextMenu = false,
            EnableSendToShortcut = false,
            EnableStartMenuShortcuts = false,
            LaunchOnStartup = false,
        };

        installer.Apply(allOff, Options);

        Assert.Equal(
        [
            @"Software\Classes\*\shell\VirusTotalOneClick",
            @"Software\Classes\Directory\shell\VirusTotalOneClick",
        ], registry.DeletedTrees);
        Assert.Empty(registry.SetValues);
        Assert.Equal(1, sendTo.DeleteCount);
        Assert.Equal(1, startMenu.DeleteAllCount);
        Assert.Equal(1, startup.DeleteCount);
    }
}
