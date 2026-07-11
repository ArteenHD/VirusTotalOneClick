using System.Diagnostics;
using VirusTotalOneClick.UI;

namespace VirusTotalOneClick.AppTests;

public class ExternalUrlLauncherTests
{
    [Theory]
    [InlineData("https://www.virustotal.com/gui/file/abc123")]
    [InlineData("https://www.virustotal.com/gui/file-analysis/xyz")]
    [InlineData("https://WWW.VIRUSTOTAL.COM/gui/analysis/123")]
    public void ResultUrlsOnTheVirusTotalGuiAreOpenable(string url)
    {
        Assert.True(new ExternalUrlLauncher(_ => { }).CanOpen(url));
    }

    [Theory]
    [InlineData("http://www.virustotal.com/gui/file/abc")]
    [InlineData("https://www.virustotal.com/api/v3/files")]
    [InlineData("https://evil.example/gui/file/abc")]
    [InlineData("https://www.virustotal.com.evil.example/gui/file/abc")]
    [InlineData("file:///C:/windows/system32/calc.exe")]
    [InlineData("not a url")]
    [InlineData("")]
    [InlineData(null)]
    public void AnythingElseIsRejected(string? url)
    {
        Assert.False(new ExternalUrlLauncher(_ => { }).CanOpen(url));
    }

    [Fact]
    public void OpenLaunchesThroughTheShell()
    {
        ProcessStartInfo? started = null;
        var launcher = new ExternalUrlLauncher(startInfo => started = startInfo);

        launcher.Open("https://www.virustotal.com/gui/file/abc123");

        Assert.NotNull(started);
        Assert.Equal("https://www.virustotal.com/gui/file/abc123", started.FileName);
        Assert.True(started.UseShellExecute);
    }

    [Fact]
    public void OpenReEnforcesTheAllowlistItself()
    {
        var started = false;
        var launcher = new ExternalUrlLauncher(_ => started = true);

        var thrown = Assert.Throws<InvalidOperationException>(
            () => launcher.Open("https://evil.example/gui/file/abc"));

        Assert.Contains("VirusTotal", thrown.Message, StringComparison.Ordinal);
        Assert.False(started);
    }
}
