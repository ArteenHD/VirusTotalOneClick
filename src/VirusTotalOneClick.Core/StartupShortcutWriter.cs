namespace VirusTotalOneClick.Core;

public sealed class StartupShortcutWriter : IStartupShortcutWriter
{
    private readonly string _startupDirectory;

    public StartupShortcutWriter()
        : this(StartupShortcutService.GetStartupDirectory())
    {
    }

    public StartupShortcutWriter(string startupDirectory)
    {
        _startupDirectory = startupDirectory;
    }

    public void Create(ShortcutRequest request)
    {
        Directory.CreateDirectory(_startupDirectory);
        WindowsShortcutFile.Create(
            Path.Combine(_startupDirectory, request.ShortcutName),
            request.ExecutablePath,
            request.Arguments);
    }

    public void Delete()
    {
        var shortcutPath = Path.Combine(_startupDirectory, StartupShortcutService.ShortcutName);
        if (File.Exists(shortcutPath))
        {
            File.Delete(shortcutPath);
        }
    }
}
