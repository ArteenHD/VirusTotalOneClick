namespace VirusTotalOneClick.Core.Tests;

public class CommandLineParserTests
{
    [Fact]
    public void NoArgumentsOpensInteractiveMode()
    {
        var options = CommandLineParser.Parse([]);

        Assert.Equal(AppMode.Interactive, options.Mode);
        Assert.Empty(options.Paths);
    }

    [Fact]
    public void UploadWithAPathFlagCapturesThePath()
    {
        var options = CommandLineParser.Parse(["upload", "--path", @"C:\Temp\a file.exe"]);

        Assert.Equal(AppMode.Upload, options.Mode);
        Assert.Equal([@"C:\Temp\a file.exe"], options.Paths);
    }

    [Fact]
    public void RepeatedPathFlagsAccumulate()
    {
        var options = CommandLineParser.Parse(["upload", "--path", "a.exe", "--path", "b.exe"]);

        Assert.Equal([@"a.exe", "b.exe"], options.Paths);
    }

    [Fact]
    public void PositionalArgumentsAfterUploadAreCaptured()
    {
        var options = CommandLineParser.Parse(["upload", "a.exe", "b.exe"]);

        Assert.Equal(AppMode.Upload, options.Mode);
        Assert.Equal(["a.exe", "b.exe"], options.Paths);
    }

    [Fact]
    public void PathFlagsAndPositionalArgumentsMixInOneInvocation()
    {
        var options = CommandLineParser.Parse(["upload", "--path", "a.exe", "b.exe"]);

        Assert.Equal(["a.exe", "b.exe"], options.Paths);
    }

    [Theory]
    [InlineData("settings", AppMode.Settings)]
    [InlineData("history", AppMode.History)]
    [InlineData("install-shell", AppMode.InstallShell)]
    [InlineData("uninstall-shell", AppMode.UninstallShell)]
    public void CommandVerbsRouteToTheirModes(string verb, AppMode expected)
    {
        var options = CommandLineParser.Parse([verb]);

        Assert.Equal(expected, options.Mode);
        Assert.Empty(options.Paths);
    }

    [Theory]
    [InlineData("UPLOAD")]
    [InlineData("Upload")]
    public void VerbsMatchCaseInsensitively(string verb)
    {
        Assert.Equal(AppMode.Upload, CommandLineParser.Parse([verb]).Mode);
    }

    [Fact]
    public void UploadWithoutPathsNeverInventsASelection()
    {
        var options = CommandLineParser.Parse(["upload"]);

        Assert.Equal(AppMode.Upload, options.Mode);
        Assert.Empty(options.Paths);
    }

    [Fact]
    public void AnUnknownFirstArgumentFallsBackToInteractive()
    {
        var options = CommandLineParser.Parse(["scan-everything", "a.exe"]);

        Assert.Equal(AppMode.Interactive, options.Mode);
        Assert.Empty(options.Paths);
    }

    [Fact]
    public void ATrailingPathFlagWithNoValueIsIgnored()
    {
        var options = CommandLineParser.Parse(["upload", "--path", "a.exe", "--path"]);

        Assert.Equal(["a.exe"], options.Paths);
    }

    [Fact]
    public void UnknownFlagsAreSkippedNotTreatedAsPaths()
    {
        var options = CommandLineParser.Parse(["upload", "--verbose", "a.exe"]);

        Assert.Equal(["a.exe"], options.Paths);
    }

    [Fact]
    public void ThePathFlagMatchesCaseInsensitively()
    {
        var options = CommandLineParser.Parse(["upload", "--PATH", "a.exe"]);

        Assert.Equal(["a.exe"], options.Paths);
    }
}
