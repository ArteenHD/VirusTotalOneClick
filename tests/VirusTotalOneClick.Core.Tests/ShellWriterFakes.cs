namespace VirusTotalOneClick.Core.Tests;

internal sealed class RecordingRegistryWriter : IRegistryWriter
{
    public List<ShellRegistryEntry> SetValues { get; } = [];

    public List<string> DeletedTrees { get; } = [];

    public void SetValue(ShellRegistryEntry entry) => SetValues.Add(entry);

    public void DeleteTree(string keyPath) => DeletedTrees.Add(keyPath);
}

internal sealed class ThrowingRegistryWriter : IRegistryWriter
{
    public void SetValue(ShellRegistryEntry entry) => throw new UnauthorizedAccessException("Registry access denied.");

    public void DeleteTree(string keyPath) => throw new UnauthorizedAccessException("Registry access denied.");
}

internal sealed class RecordingSendToShortcutWriter : ISendToShortcutWriter
{
    public List<ShortcutRequest> Created { get; } = [];

    public int DeleteCount { get; private set; }

    public void Create(ShortcutRequest request) => Created.Add(request);

    public void Delete() => DeleteCount++;
}

internal sealed class RecordingStartMenuShortcutWriter : IStartMenuShortcutWriter
{
    public List<ShortcutRequest> Created { get; } = [];

    public int DeleteAllCount { get; private set; }

    public void Create(ShortcutRequest request) => Created.Add(request);

    public void DeleteAll() => DeleteAllCount++;
}

internal sealed class RecordingStartupShortcutWriter : IStartupShortcutWriter
{
    public List<ShortcutRequest> Created { get; } = [];

    public int DeleteCount { get; private set; }

    public void Create(ShortcutRequest request) => Created.Add(request);

    public void Delete() => DeleteCount++;
}

internal sealed class PrefixedRegistryWriter : IRegistryWriter
{
    private readonly IRegistryWriter _inner;
    private readonly string _prefix;

    public PrefixedRegistryWriter(IRegistryWriter inner, string prefix)
    {
        _inner = inner;
        _prefix = prefix;
    }

    public void SetValue(ShellRegistryEntry entry) => _inner.SetValue(entry with { KeyPath = $@"{_prefix}\{entry.KeyPath}" });

    public void DeleteTree(string keyPath) => _inner.DeleteTree($@"{_prefix}\{keyPath}");
}
