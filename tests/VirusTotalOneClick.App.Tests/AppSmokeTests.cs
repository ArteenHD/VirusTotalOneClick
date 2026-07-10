using System.Windows;

namespace VirusTotalOneClick.AppTests;

public class AppSmokeTests
{
    [Fact]
    public void AppIsAWpfApplication()
    {
        Assert.True(typeof(App).IsSubclassOf(typeof(Application)));
    }
}
