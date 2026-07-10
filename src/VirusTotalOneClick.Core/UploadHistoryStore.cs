using System.Text.Json;

namespace VirusTotalOneClick.Core;

public sealed class UploadHistoryStore : IUploadHistoryStore
{
    private const int AppendAttempts = 5;
    private static readonly TimeSpan AppendRetryDelay = TimeSpan.FromMilliseconds(50);

    private readonly string _path;

    public UploadHistoryStore(string path)
    {
        _path = path;
    }

    public void Append(UploadHistoryEntry entry)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var line = JsonSerializer.Serialize(entry) + Environment.NewLine;

        // Appends open the file exclusively; another process appending at the same
        // time raises a sharing IOException, so retry briefly before giving up.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.AppendAllText(_path, line);
                return;
            }
            catch (IOException) when (attempt < AppendAttempts)
            {
                Thread.Sleep(AppendRetryDelay);
            }
        }
    }

    public IReadOnlyList<UploadHistoryEntry> ReadAll()
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        var entries = new List<UploadHistoryEntry>();
        foreach (var line in File.ReadLines(_path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var entry = JsonSerializer.Deserialize<UploadHistoryEntry>(line);
                if (entry is not null)
                {
                    entries.Add(entry);
                }
            }
            catch (JsonException)
            {
                // The log is append-only JSONL; a line torn by a crash mid-write
                // must not take the readable entries down with it.
            }
        }

        return entries;
    }

    public void Clear()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
