namespace VirusTotalOneClick.Core;

public sealed class NullUploadHistoryStore : IUploadHistoryStore
{
    public static NullUploadHistoryStore Instance { get; } = new();

    private NullUploadHistoryStore()
    {
    }

    public void Append(UploadHistoryEntry entry)
    {
    }

    public IReadOnlyList<UploadHistoryEntry> ReadAll() => [];

    public void Clear()
    {
    }
}
