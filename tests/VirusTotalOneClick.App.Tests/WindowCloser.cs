using System.Windows;

namespace VirusTotalOneClick.AppTests;

internal sealed class WindowCloser(Window window) : IDisposable
{
    public Window Window { get; } = window;

    public void Dispose() => Window.Close();
}
