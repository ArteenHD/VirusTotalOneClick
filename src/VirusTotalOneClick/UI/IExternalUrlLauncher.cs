namespace VirusTotalOneClick.UI;

public interface IExternalUrlLauncher
{
    bool CanOpen(string? url);

    void Open(string url);
}
