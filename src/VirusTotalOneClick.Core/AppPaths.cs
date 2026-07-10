namespace VirusTotalOneClick.Core;

public sealed record AppPaths(string AppDataDirectory, string SettingsPath, string HistoryPath)
{
    public static AppPaths FromCurrentUser()
    {
        return FromRoot(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
    }

    public static AppPaths FromRoot(string applicationDataRoot)
    {
        var appDataDirectory = Path.Combine(applicationDataRoot, "VirusTotalOneClick");
        return new AppPaths(
            appDataDirectory,
            Path.Combine(appDataDirectory, "settings.json"),
            Path.Combine(appDataDirectory, "history.jsonl"));
    }
}
