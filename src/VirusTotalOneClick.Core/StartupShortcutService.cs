namespace VirusTotalOneClick.Core;

public static class StartupShortcutService
{
    public const string ShortcutName = "VirusTotal OneClick Settings.lnk";

    public static ShortcutRequest BuildShortcutRequest(string executablePath)
        => new(executablePath, "settings", ShortcutName);

    public static string GetStartupDirectory()
        => GetStartupDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

    public static string GetStartupDirectory(string applicationDataDirectory)
        => Path.Combine(applicationDataDirectory, "Microsoft", "Windows", "Start Menu", "Programs", "Startup");
}
