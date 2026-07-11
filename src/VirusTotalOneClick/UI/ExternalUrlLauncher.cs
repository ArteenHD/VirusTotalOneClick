using System.Diagnostics;

namespace VirusTotalOneClick.UI;

/// <summary>
/// Opens upload-result links in the default browser. Only HTTPS URLs on
/// www.virustotal.com under /gui/ are ever launched — result URLs come back
/// from the network, so everything else is refused outright.
/// </summary>
public sealed class ExternalUrlLauncher : IExternalUrlLauncher
{
    private readonly Action<ProcessStartInfo> _start;

    public ExternalUrlLauncher(Action<ProcessStartInfo>? start = null)
    {
        _start = start ?? (startInfo => Process.Start(startInfo));
    }

    public bool CanOpen(string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && string.Equals(uri.Host, "www.virustotal.com", StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.StartsWith("/gui/", StringComparison.Ordinal);
    }

    public void Open(string url)
    {
        if (!CanOpen(url))
        {
            throw new InvalidOperationException("Only VirusTotal result URLs can be opened.");
        }

        _start(new ProcessStartInfo(url)
        {
            UseShellExecute = true,
        });
    }
}
