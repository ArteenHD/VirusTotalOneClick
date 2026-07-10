namespace VirusTotalOneClick.Core;

public interface IUploadHistoryStore
{
    void Append(UploadHistoryEntry entry);

    IReadOnlyList<UploadHistoryEntry> ReadAll();

    void Clear();
}
