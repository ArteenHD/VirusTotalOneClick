using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace VirusTotalOneClick.UI;

/// <summary>
/// Chrome shared by every window: Mica backdrop, content extended into the
/// title bar, centered startup, and a live watch on the OS theme for the
/// window's lifetime.
/// </summary>
public class AppWindow : FluentWindow
{
    public AppWindow()
    {
        Title = "VirusTotal OneClick";
        WindowBackdropType = WindowBackdropType.Mica;
        ExtendsContentIntoTitleBar = true;
        WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
        SnapsToDevicePixels = true;

        SystemThemeWatcher.Watch(this);
        Closed += (_, _) =>
        {
            try
            {
                SystemThemeWatcher.UnWatch(this);
            }
            catch (InvalidOperationException)
            {
                // The window handle may already be gone at close time.
            }
        };
    }
}
