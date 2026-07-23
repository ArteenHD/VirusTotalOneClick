using System.Reflection;

namespace VirusTotalOneClick.Core.Tests;

public class AssemblyVersionTests
{
    [Fact]
    public void CoreAssemblyVersionMatchesProductVersion()
    {
        var coreAssembly = Assembly.Load("VirusTotalOneClick.Core");
        var testAssembly = Assembly.GetExecutingAssembly();

        Assert.Equal(testAssembly.GetName().Version, coreAssembly.GetName().Version);
    }
}
