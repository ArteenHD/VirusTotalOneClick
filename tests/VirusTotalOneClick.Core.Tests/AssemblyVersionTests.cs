using System.Reflection;

namespace VirusTotalOneClick.Core.Tests;

public class AssemblyVersionTests
{
    [Fact]
    public void CoreAssemblyVersionMatchesProductVersion()
    {
        var assembly = Assembly.Load("VirusTotalOneClick.Core");

        Assert.Equal(new Version(0, 1, 0, 0), assembly.GetName().Version);
    }
}
