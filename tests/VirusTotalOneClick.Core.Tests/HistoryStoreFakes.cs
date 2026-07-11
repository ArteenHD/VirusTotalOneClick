namespace VirusTotalOneClick.Core.Tests;

internal sealed class RecordingHistoryStore : IUploadHistoryStore
{
    public List<UploadHistoryEntry> Appended { get; } = [];

    public void Append(UploadHistoryEntry entry) => Appended.Add(entry);

    public IReadOnlyList<UploadHistoryEntry> ReadAll() => Appended.ToList();

    public void Clear() => Appended.Clear();
}

internal sealed class ThrowingHistoryStore : IUploadHistoryStore
{
    public void Append(UploadHistoryEntry entry) => throw new IOException("History file is locked.");

    public IReadOnlyList<UploadHistoryEntry> ReadAll() => throw new IOException("History file is locked.");

    public void Clear() => throw new IOException("History file is locked.");
}
