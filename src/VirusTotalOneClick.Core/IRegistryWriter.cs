namespace VirusTotalOneClick.Core;

public interface IRegistryWriter
{
    void SetValue(ShellRegistryEntry entry);

    void DeleteTree(string keyPath);
}
