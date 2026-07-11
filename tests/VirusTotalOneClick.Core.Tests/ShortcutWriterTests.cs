namespace VirusTotalOneClick.Core.Tests;

public class ShortcutWriterTests
{
    [Fact]
    public void SendToWriterCreatesARealShortcutThatRoundTrips()
    {
        StaThread.Run(() =>
        {
            using var folder = new TemporaryFolder();
            var exePath = folder.CreateFile("VirusTotalOneClick.exe", "stub");
            var sendToDirectory = Path.Combine(folder.Path, "SendTo");
            var writer = new SendToShortcutWriter(sendToDirectory);

            writer.Create(new ShortcutRequest(exePath, SendToShortcutService.Arguments, SendToShortcutService.ShortcutName));

            var shortcutPath = Path.Combine(sendToDirectory, "Upload to VirusTotal.lnk");
            Assert.True(File.Exists(shortcutPath));
            var link = ShellLinkReader.Read(shortcutPath);
            Assert.Equal(exePath, link.TargetPath, ignoreCase: true);
            Assert.Equal("upload", link.Arguments);
            Assert.Equal(exePath, link.IconPath, ignoreCase: true);
            Assert.Equal(0, link.IconIndex);

            writer.Delete();
            Assert.False(File.Exists(shortcutPath));
            writer.Delete();
        });
    }

    [Fact]
    public void StartMenuWriterCreatesTheProductShortcut()
    {
        StaThread.Run(() =>
        {
            using var folder = new TemporaryFolder();
            var exePath = folder.CreateFile("VirusTotalOneClick.exe", "stub");
            var startMenuDirectory = Path.Combine(folder.Path, "StartMenu");
            var writer = new StartMenuShortcutWriter(startMenuDirectory);

            foreach (var request in StartMenuShortcutService.BuildShortcutRequests(exePath))
            {
                writer.Create(request);
            }

            var shortcutPath = Path.Combine(startMenuDirectory, "VirusTotal OneClick.lnk");
            var link = ShellLinkReader.Read(shortcutPath);
            Assert.Equal(exePath, link.TargetPath, ignoreCase: true);
            Assert.Equal(string.Empty, link.Arguments);
        });
    }

    [Fact]
    public void DisablingStartMenuRemovesOnlyOurShortcutAndTheFolderOnlyWhenEmpty()
    {
        StaThread.Run(() =>
        {
            using var folder = new TemporaryFolder();
            var exePath = folder.CreateFile("VirusTotalOneClick.exe", "stub");
            var startMenuDirectory = Path.Combine(folder.Path, "StartMenu");
            var writer = new StartMenuShortcutWriter(startMenuDirectory);
            foreach (var request in StartMenuShortcutService.BuildShortcutRequests(exePath))
            {
                writer.Create(request);
            }
            var foreignFile = Path.Combine(startMenuDirectory, "unrelated-notes.txt");
            File.WriteAllText(foreignFile, "someone else's file");

            writer.DeleteAll();

            Assert.False(File.Exists(Path.Combine(startMenuDirectory, "VirusTotal OneClick.lnk")));
            Assert.True(File.Exists(foreignFile));
            Assert.True(Directory.Exists(startMenuDirectory));

            File.Delete(foreignFile);
            writer.DeleteAll();

            Assert.False(Directory.Exists(startMenuDirectory));
        });
    }

    [Fact]
    public void DeleteAllIsANoOpWhenTheFolderNeverExisted()
    {
        using var folder = new TemporaryFolder();
        var writer = new StartMenuShortcutWriter(Path.Combine(folder.Path, "never-created"));

        writer.DeleteAll();
    }

    [Fact]
    public void StartupWriterCreatesAndDeletesTheSettingsShortcut()
    {
        StaThread.Run(() =>
        {
            using var folder = new TemporaryFolder();
            var exePath = folder.CreateFile("VirusTotalOneClick.exe", "stub");
            var startupDirectory = Path.Combine(folder.Path, "Startup");
            var writer = new StartupShortcutWriter(startupDirectory);

            writer.Create(StartupShortcutService.BuildShortcutRequest(exePath));

            var shortcutPath = Path.Combine(startupDirectory, "VirusTotal OneClick Settings.lnk");
            var link = ShellLinkReader.Read(shortcutPath);
            Assert.Equal("settings", link.Arguments);

            writer.Delete();
            Assert.False(File.Exists(shortcutPath));
            writer.Delete();
        });
    }
}
