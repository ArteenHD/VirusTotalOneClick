namespace VirusTotalOneClick.Core;

public sealed class ShellIntegrationInstaller : IShellIntegrationApplier
{
    private static readonly string[] ClassicMenuKeys =
    [
        $@"Software\Classes\*\shell\{ShellIntegrationService.MenuKeyName}",
        $@"Software\Classes\Directory\shell\{ShellIntegrationService.MenuKeyName}",
    ];

    private readonly IRegistryWriter _registryWriter;
    private readonly ISendToShortcutWriter _sendToShortcutWriter;
    private readonly IStartMenuShortcutWriter _startMenuShortcutWriter;
    private readonly IStartupShortcutWriter _startupShortcutWriter;

    public ShellIntegrationInstaller(
        IRegistryWriter registryWriter,
        ISendToShortcutWriter sendToShortcutWriter,
        IStartMenuShortcutWriter startMenuShortcutWriter,
        IStartupShortcutWriter startupShortcutWriter)
    {
        _registryWriter = registryWriter;
        _sendToShortcutWriter = sendToShortcutWriter;
        _startMenuShortcutWriter = startMenuShortcutWriter;
        _startupShortcutWriter = startupShortcutWriter;
    }

    public void Apply(AppSettings settings, ShellIntegrationOptions options)
    {
        List<Exception>? failures = null;

        // Each surface applies independently: one failing step must never block
        // the others, and every failure is surfaced together at the end.
        try
        {
            if (settings.EnableClassicContextMenu)
            {
                foreach (var entry in ShellIntegrationService.BuildRegistryEntries(options))
                {
                    _registryWriter.SetValue(entry);
                }
            }
            else
            {
                foreach (var key in ClassicMenuKeys)
                {
                    _registryWriter.DeleteTree(key);
                }
            }
        }
        catch (Exception ex)
        {
            (failures ??= []).Add(ex);
        }

        try
        {
            if (settings.EnableSendToShortcut)
            {
                _sendToShortcutWriter.Create(new ShortcutRequest(
                    options.ExecutablePath,
                    SendToShortcutService.Arguments,
                    SendToShortcutService.ShortcutName));
            }
            else
            {
                _sendToShortcutWriter.Delete();
            }
        }
        catch (Exception ex)
        {
            (failures ??= []).Add(ex);
        }

        try
        {
            if (settings.EnableStartMenuShortcuts)
            {
                foreach (var request in StartMenuShortcutService.BuildShortcutRequests(options.ExecutablePath))
                {
                    _startMenuShortcutWriter.Create(request);
                }
            }
            else
            {
                _startMenuShortcutWriter.DeleteAll();
            }
        }
        catch (Exception ex)
        {
            (failures ??= []).Add(ex);
        }

        try
        {
            if (settings.LaunchOnStartup)
            {
                _startupShortcutWriter.Create(StartupShortcutService.BuildShortcutRequest(options.ExecutablePath));
            }
            else
            {
                _startupShortcutWriter.Delete();
            }
        }
        catch (Exception ex)
        {
            (failures ??= []).Add(ex);
        }

        if (failures is { Count: > 0 })
        {
            throw new AggregateException("One or more shell integration steps failed.", failures);
        }
    }
}
