using System.Windows;
using VirusTotalOneClick.Core;

namespace VirusTotalOneClick;

public partial class App
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var options = CommandLineParser.Parse(e.Args);
        if (AppBootstrapper.TryRunNonInteractive(options))
        {
            Shutdown();
            return;
        }

        // Only the command verbs are serviceable so far; the interactive
        // windows are still to come.
        Shutdown();
    }
}
