namespace VirusTotalOneClick.Core;

public static class StartMenuShortcutService
{
    public const string ProductFolderName = "VirusTotal OneClick";

    public static readonly IReadOnlyList<string> ShortcutNames = ["VirusTotal OneClick.lnk"];

    public static IReadOnlyList<ShortcutRequest> BuildShortcutRequests(string executablePath)
        => ShortcutNames.Select(name => new ShortcutRequest(executablePath, string.Empty, name)).ToList();

    public static string GetStartMenuDirectory()
        => GetStartMenuDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

    public static string GetStartMenuDirectory(string applicationDataDirectory)
        => Path.Combine(applicationDataDirectory, "Microsoft", "Windows", "Start Menu", "Programs", ProductFolderName);
}
