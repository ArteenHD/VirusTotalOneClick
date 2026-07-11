namespace VirusTotalOneClick.Core;

public static class ShellIntegrationService
{
    public const string MenuKeyName = "VirusTotalOneClick";

    public static string BuildCommand(string executablePath)
    {
        if (executablePath.Contains('"'))
        {
            throw new ArgumentException("Executable path cannot contain quotes.", nameof(executablePath));
        }

        return $"\"{executablePath}\" upload --path \"%1\"";
    }

    public static IReadOnlyList<ShellRegistryEntry> BuildRegistryEntries(ShellIntegrationOptions options)
    {
        var command = BuildCommand(options.ExecutablePath);
        var icon = $"\"{options.ExecutablePath}\",0";
        return
        [
            new ShellRegistryEntry($@"Software\Classes\*\shell\{MenuKeyName}", null, options.MenuText),
            new ShellRegistryEntry($@"Software\Classes\*\shell\{MenuKeyName}", "Icon", icon),
            new ShellRegistryEntry($@"Software\Classes\*\shell\{MenuKeyName}", "MultiSelectModel", "Single"),
            new ShellRegistryEntry($@"Software\Classes\*\shell\{MenuKeyName}\command", null, command),
            new ShellRegistryEntry($@"Software\Classes\Directory\shell\{MenuKeyName}", null, options.MenuText),
            new ShellRegistryEntry($@"Software\Classes\Directory\shell\{MenuKeyName}", "Icon", icon),
            new ShellRegistryEntry($@"Software\Classes\Directory\shell\{MenuKeyName}", "MultiSelectModel", "Single"),
            new ShellRegistryEntry($@"Software\Classes\Directory\shell\{MenuKeyName}\command", null, command),
        ];
    }
}
