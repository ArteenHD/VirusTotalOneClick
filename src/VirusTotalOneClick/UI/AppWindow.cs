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

    /// <summary>
    /// Margin left below the clamped height so a small-screen window doesn't
    /// sit flush against the work area's edge (typically the taskbar).
    /// </summary>
    private const double WorkAreaMargin = 24;

    /// <summary>
    /// The startup height to use: the window's own design height, or the
    /// current work area's height (less a small margin) when that is
    /// smaller — so a fixed-height dialog never opens with its footer
    /// button below a small or scaled-up-DPI screen's usable area. Never
    /// clamps below <paramref name="minHeight"/>, the window's own floor.
    /// </summary>
    internal static double ClampHeightToWorkArea(double designHeight, double workAreaHeight, double minHeight)
    {
        if (workAreaHeight <= 0 || workAreaHeight >= designHeight)
        {
            return designHeight;
        }

        return Math.Max(workAreaHeight - WorkAreaMargin, minHeight);
    }
}
