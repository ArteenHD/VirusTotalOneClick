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
        // time raises a sharing IOException, so retry briefly before giving up. The
        // check for a missing trailing newline shares the same retry: a crash
        // mid-append can leave a partial line, and gluing the new record onto that
        // torn tail would lose both lines when readers skip the malformed one.
        RetryOnSharingViolation(() =>
        {
            if (FileEndsWithoutNewline())
            {
                File.AppendAllText(_path, Environment.NewLine);
            }

            File.AppendAllText(_path, line);
        });
    }

    private bool FileEndsWithoutNewline()
    {
        if (!File.Exists(_path))
        {
            return false;
        }

        using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (stream.Length == 0)
        {
            return false;
        }

        stream.Seek(-1, SeekOrigin.End);
        return stream.ReadByte() != '\n';
    }

    public IReadOnlyList<UploadHistoryEntry> ReadAll()
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        var entries = new List<UploadHistoryEntry>();

        // File.ReadLines opens for FileShare.Read only, which conflicts with a
        // concurrent appender's write handle; ask for ReadWrite so reads never
        // contend with appends in progress.
        using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
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
        if (!File.Exists(_path))
        {
            return;
        }

        // A concurrent appender can hold the file exclusively for the same brief
        // window Append tolerates, so retry the same way.
        RetryOnSharingViolation(() => File.Delete(_path));
    }

    private static void RetryOnSharingViolation(Action action)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (IOException) when (attempt < AppendAttempts)
            {
                Thread.Sleep(AppendRetryDelay);
            }
        }
    }
}
