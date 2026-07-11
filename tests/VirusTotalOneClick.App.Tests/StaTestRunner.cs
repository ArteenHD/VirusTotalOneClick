using System.Collections.Concurrent;
using System.Windows;

namespace VirusTotalOneClick.AppTests;

/// <summary>
/// Runs window-constructing test bodies on one shared, lazily-created STA
/// thread hosting a real WPF Application with the app's resource dictionaries
/// merged, and rethrows failures on the xunit thread.
/// </summary>
internal static class StaTestRunner
{
    private static readonly Lock AppLock = new();
    private static bool _appInitialized;
    private static BlockingCollection<Action>? _workItems;

    public static void Run(Action action)
    {
        EnsureStaThread();

        Exception? exception = null;
        using var completed = new ManualResetEventSlim();

        _workItems!.Add(() =>
        {
            try
            {
                EnsureApplication();
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                completed.Set();
            }
        });

        completed.Wait();

        if (exception is not null)
        {
            throw exception;
        }
    }

    private static void EnsureStaThread()
    {
        if (_workItems is not null)
        {
            return;
        }

        lock (AppLock)
        {
            if (_workItems is not null)
            {
                return;
            }

            _workItems = [];
            using var ready = new ManualResetEventSlim();
            var thread = new Thread(() =>
            {
                ready.Set();
                foreach (var workItem in _workItems.GetConsumingEnumerable())
                {
                    workItem();
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            ready.Wait();
        }
    }

    private static void EnsureApplication()
    {
        if (_appInitialized)
        {
            return;
        }

        lock (AppLock)
        {
            if (_appInitialized)
            {
                return;
            }

            if (Application.Current is null)
            {
                _ = new Application
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown,
                };
            }

            // App.xaml's root is the Application element itself, so loading it
            // as a plain ResourceDictionary re-triggers the App's constructor
            // and collides with the Application instance created above.
            // Merge the same dictionaries the app merges instead.
            Application.Current!.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary());
            Application.Current.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
            Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/VirusTotalOneClick;component/Themes/Tokens.xaml", UriKind.Absolute),
            });

            _appInitialized = true;
        }
    }
}
