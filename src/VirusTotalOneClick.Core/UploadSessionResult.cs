namespace VirusTotalOneClick.Core;

public sealed record UploadSessionResult(
    int ItemCount,
    int UploadedCount,
    int FailedCount,
    IReadOnlyList<UploadSessionItemResult> ItemResults,
    bool IsCanceled = false);
