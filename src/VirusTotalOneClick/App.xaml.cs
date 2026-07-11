using System.Windows;
using VirusTotalOneClick.Core;
using Wpf.Ui.Appearance;

namespace VirusTotalOneClick;

public partial class App
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // The app follows the user's Windows theme and accent; there is no
        // brand color override anywhere.
        ApplicationThemeManager.ApplySystemTheme();

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
