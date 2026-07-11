namespace VirusTotalOneClick.Core;

public sealed class StartMenuShortcutWriter : IStartMenuShortcutWriter
{
    private readonly string _startMenuDirectory;

    public StartMenuShortcutWriter()
        : this(StartMenuShortcutService.GetStartMenuDirectory())
    {
    }

    public StartMenuShortcutWriter(string startMenuDirectory)
    {
        _startMenuDirectory = startMenuDirectory;
    }

    public void Create(ShortcutRequest request)
    {
        Directory.CreateDirectory(_startMenuDirectory);
        WindowsShortcutFile.Create(
            Path.Combine(_startMenuDirectory, request.ShortcutName),
            request.ExecutablePath,
            request.Arguments);
    }

    public void DeleteAll()
    {
        if (!Directory.Exists(_startMenuDirectory))
        {
            return;
        }

        foreach (var name in StartMenuShortcutService.ShortcutNames)
        {
            var shortcutPath = Path.Combine(_startMenuDirectory, name);
            if (File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
            }
        }

        if (!Directory.EnumerateFileSystemEntries(_startMenuDirectory).Any())
        {
            Directory.Delete(_startMenuDirectory);
        }
    }
}
