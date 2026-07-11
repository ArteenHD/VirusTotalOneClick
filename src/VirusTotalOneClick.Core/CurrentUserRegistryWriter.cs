using Microsoft.Win32;

namespace VirusTotalOneClick.Core;

public sealed class CurrentUserRegistryWriter : IRegistryWriter
{
    public void SetValue(ShellRegistryEntry entry)
    {
        using var key = Registry.CurrentUser.CreateSubKey(entry.KeyPath);
        key.SetValue(NormalizeValueName(entry.ValueName), entry.Value, RegistryValueKind.String);
    }

    public void DeleteTree(string keyPath)
    {
        Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
    }

    public static string NormalizeValueName(string? valueName)
    {
        return valueName ?? string.Empty;
    }
}
