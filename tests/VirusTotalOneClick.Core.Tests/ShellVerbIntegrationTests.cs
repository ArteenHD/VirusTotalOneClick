using Microsoft.Win32;

namespace VirusTotalOneClick.Core.Tests;

public sealed class ShellVerbIntegrationTests : IDisposable
{
    private readonly string _sandboxKeyPath = $@"Software\VirusTotalOneClick.Tests\{Guid.NewGuid():N}";
    private readonly TemporaryFolder _folder = new();

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_sandboxKeyPath, throwOnMissingSubKey: false);
        using (var parent = Registry.CurrentUser.OpenSubKey(@"Software\VirusTotalOneClick.Tests", writable: true))
        {
            if (parent is not null && parent.SubKeyCount == 0 && parent.ValueCount == 0)
            {
                Registry.CurrentUser.DeleteSubKey(@"Software\VirusTotalOneClick.Tests", throwOnMissingSubKey: false);
            }
        }

        _folder.Dispose();
    }

    [Fact]
    public void InstallAndUninstallShellVerbsRoundTripThroughTheRealRegistryAndRealShortcuts()
    {
        StaThread.Run(() =>
        {
            var exePath = _folder.CreateFile("VirusTotalOneClick.exe", "stub");
            var sendToDirectory = Path.Combine(_folder.Path, "SendTo");
            var startMenuDirectory = Path.Combine(_folder.Path, "StartMenu");
            var startupDirectory = Path.Combine(_folder.Path, "Startup");
            var installer = new ShellIntegrationInstaller(
                new PrefixedRegistryWriter(new CurrentUserRegistryWriter(), _sandboxKeyPath),
                new SendToShortcutWriter(sendToDirectory),
                new StartMenuShortcutWriter(startMenuDirectory),
                new StartupShortcutWriter(startupDirectory));
            var runner = new ShellIntegrationCommandRunner(installer, new ShellIntegrationOptions(exePath, "Upload to VirusTotal"));

            Assert.True(runner.TryRun(AppMode.InstallShell));

            using (var menuKey = Registry.CurrentUser.OpenSubKey($@"{_sandboxKeyPath}\Software\Classes\*\shell\VirusTotalOneClick"))
            {
                Assert.NotNull(menuKey);
                Assert.Equal("Upload to VirusTotal", menuKey!.GetValue(null));
                Assert.Equal($"\"{exePath}\",0", menuKey.GetValue("Icon"));
                Assert.Equal("Single", menuKey.GetValue("MultiSelectModel"));
            }

            using (var commandKey = Registry.CurrentUser.OpenSubKey($@"{_sandboxKeyPath}\Software\Classes\*\shell\VirusTotalOneClick\command"))
            {
                Assert.NotNull(commandKey);
                Assert.Equal($"\"{exePath}\" upload --path \"%1\"", commandKey!.GetValue(null));
            }

            using (var directoryTwin = Registry.CurrentUser.OpenSubKey($@"{_sandboxKeyPath}\Software\Classes\Directory\shell\VirusTotalOneClick\command"))
            {
                Assert.NotNull(directoryTwin);
            }

            var sendToLink = ShellLinkReader.Read(Path.Combine(sendToDirectory, "Upload to VirusTotal.lnk"));
            Assert.Equal("upload", sendToLink.Arguments);
            Assert.True(File.Exists(Path.Combine(startMenuDirectory, "VirusTotal OneClick.lnk")));
            Assert.False(File.Exists(Path.Combine(startupDirectory, "VirusTotal OneClick Settings.lnk")));

            Assert.True(runner.TryRun(AppMode.UninstallShell));

            Assert.Null(Registry.CurrentUser.OpenSubKey($@"{_sandboxKeyPath}\Software\Classes\*\shell\VirusTotalOneClick"));
            Assert.Null(Registry.CurrentUser.OpenSubKey($@"{_sandboxKeyPath}\Software\Classes\Directory\shell\VirusTotalOneClick"));
            Assert.False(File.Exists(Path.Combine(sendToDirectory, "Upload to VirusTotal.lnk")));
            Assert.False(Directory.Exists(startMenuDirectory));
        });
    }
}
