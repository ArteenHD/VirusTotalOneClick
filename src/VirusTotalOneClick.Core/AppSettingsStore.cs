using System.Text.Json;

namespace VirusTotalOneClick.Core;

public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
    };

    // A file that exists but can't be read or parsed is not a fresh install; the
    // user's consent state is unknown, so confirm before uploading rather than
    // silently falling back to the auto-upload default.
    private static readonly AppSettings UnreadableFileDefaults = AppSettings.Default with
    {
        ConfirmBeforeFileUpload = true,
    };

    private readonly string _path;

    public AppSettingsStore(string path)
    {
        _path = path;
    }

    public AppSettings Load()
    {
        if (!File.Exists(_path))
        {
            return AppSettings.Default;
        }

        try
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? UnreadableFileDefaults;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return UnreadableFileDefaults;
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var tempPath = _path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, WriteOptions));
        File.Move(tempPath, _path, overwrite: true);
    }
}
