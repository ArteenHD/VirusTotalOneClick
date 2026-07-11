namespace VirusTotalOneClick.Core;

public static class SendToShortcutService
{
    public const string ShortcutName = "Upload to VirusTotal.lnk";

    // Explorer appends the selected paths itself when invoking a Send To shortcut.
    public const string Arguments = "upload";

    public static string GetSendToDirectory()
        => GetSendToDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

    public static string GetSendToDirectory(string applicationDataDirectory)
        => Path.Combine(applicationDataDirectory, "Microsoft", "Windows", "SendTo");
}
