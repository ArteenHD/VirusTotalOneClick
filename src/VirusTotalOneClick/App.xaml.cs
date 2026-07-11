using System.Windows;
using VirusTotalOneClick.Core;
using VirusTotalOneClick.UI;
using Wpf.Ui.Appearance;

namespace VirusTotalOneClick;

public partial class App
{
    private CoalescedUploadSession? _uploadSession;

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

        var composition = AppBootstrapper.Compose(options, AppPaths.FromCurrentUser());
        var factory = new WindowFactory(composition);

        if (composition.Options.Mode == AppMode.Upload)
        {
            StartUploadSession(factory, composition.Options.Paths);
            return;
        }

        ShowWindow(factory.Create(composition.Options));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _uploadSession?.Dispose();
        base.OnExit(e);
    }

    private void StartUploadSession(WindowFactory factory, IReadOnlyList<string> paths)
    {
        // Explorer starts one process per selected item; a mutex elects one
        // primary and everyone else hands their paths over and exits, so a
        // multi-select becomes one review or one queue instead of a window
        // per file. Only upload launches take part — settings, history, and
        // the shell verbs never coalesce.
        var session = new CoalescedUploadSession();
        if (session.TryBecomePrimary())
        {
            _uploadSession = session;
            session.SeedAndPump(paths, batch => Dispatcher.Invoke(() =>
                ShowWindow(factory.Create(new CommandLineOptions(AppMode.Upload, batch)))));
            return;
        }

        var forwarded = session.TryForwardAsync(paths, CancellationToken.None)
            .GetAwaiter().GetResult();
        session.Dispose();
        if (forwarded)
        {
            Shutdown();
            return;
        }

        // The primary vanished between election and forwarding; this launch
        // still serves the user standalone rather than dropping their files.
        ShowWindow(factory.Create(new CommandLineOptions(AppMode.Upload, paths)));
    }

    private void ShowWindow(Window window)
    {
        MainWindow ??= window;
        window.Show();
    }
}
