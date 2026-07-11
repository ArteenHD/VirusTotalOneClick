namespace VirusTotalOneClick.Core;

public sealed class ShellIntegrationCommandRunner
{
    private readonly IShellIntegrationApplier _applier;
    private readonly ShellIntegrationOptions _options;
    private readonly Func<AppSettings>? _loadSettings;

    public ShellIntegrationCommandRunner(
        IShellIntegrationApplier applier,
        ShellIntegrationOptions options,
        Func<AppSettings>? loadSettings = null)
    {
        _applier = applier;
        _options = options;
        _loadSettings = loadSettings;
    }

    public bool TryRun(AppMode mode)
    {
        if (mode == AppMode.InstallShell)
        {
            _applier.Apply(_loadSettings?.Invoke() ?? AppSettings.Default, _options);
            return true;
        }

        if (mode == AppMode.UninstallShell)
        {
            _applier.Apply(AppSettings.Default with
            {
                EnableClassicContextMenu = false,
                EnableSendToShortcut = false,
                EnableStartMenuShortcuts = false,
                LaunchOnStartup = false,
            }, _options);
            return true;
        }

        return false;
    }
}
