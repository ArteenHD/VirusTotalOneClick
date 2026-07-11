namespace VirusTotalOneClick.Core.Tests;

public class CoalescingScopeTests
{
    [Theory]
    [InlineData(AppMode.Interactive, false)]
    [InlineData(AppMode.Settings, false)]
    [InlineData(AppMode.History, false)]
    [InlineData(AppMode.Upload, true)]
    [InlineData(AppMode.InstallShell, false)]
    [InlineData(AppMode.UninstallShell, false)]
    public void OnlyUploadLaunchesShareAnInstance(AppMode mode, bool expected)
    {
        Assert.Equal(expected, CoalescingScope.AppliesTo(mode));
    }
}
