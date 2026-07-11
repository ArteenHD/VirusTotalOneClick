namespace VirusTotalOneClick.Core.Tests;

public class ShortcutServicesTests
{
    [Fact]
    public void SendToShortcutIsNamedUploadToVirusTotal()
    {
        Assert.Equal("Upload to VirusTotal.lnk", SendToShortcutService.ShortcutName);
    }

    [Fact]
    public void SendToShortcutLaunchesWithTheBareUploadVerb()
    {
        Assert.Equal("upload", SendToShortcutService.Arguments);
    }

    [Fact]
    public void SendToDirectoryResolvesUnderTheGivenApplicationDataRoot()
    {
        Assert.Equal(
            @"C:\Users\someone\AppData\Roaming\Microsoft\Windows\SendTo",
            SendToShortcutService.GetSendToDirectory(@"C:\Users\someone\AppData\Roaming"));
    }

    [Fact]
    public void StartMenuHasExactlyOneShortcutWithNoArguments()
    {
        var requests = StartMenuShortcutService.BuildShortcutRequests(@"C:\Apps\VirusTotalOneClick.exe");

        var request = Assert.Single(requests);
        Assert.Equal("VirusTotal OneClick.lnk", request.ShortcutName);
        Assert.Equal(string.Empty, request.Arguments);
        Assert.Equal(@"C:\Apps\VirusTotalOneClick.exe", request.ExecutablePath);
    }

    [Fact]
    public void StartMenuDirectoryResolvesToTheProductFolder()
    {
        Assert.Equal(
            @"C:\Users\someone\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\VirusTotal OneClick",
            StartMenuShortcutService.GetStartMenuDirectory(@"C:\Users\someone\AppData\Roaming"));
    }

    [Fact]
    public void StartupShortcutLaunchesIntoSettings()
    {
        var request = StartupShortcutService.BuildShortcutRequest(@"C:\Apps\VirusTotalOneClick.exe");

        Assert.Equal("VirusTotal OneClick Settings.lnk", request.ShortcutName);
        Assert.Equal("settings", request.Arguments);
    }

    [Fact]
    public void StartupDirectoryResolvesToTheStartupFolder()
    {
        Assert.Equal(
            @"C:\Users\someone\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup",
            StartupShortcutService.GetStartupDirectory(@"C:\Users\someone\AppData\Roaming"));
    }
}
