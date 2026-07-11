namespace VirusTotalOneClick.Core;

public sealed record UploadSessionItemResult(
    string OriginalPath,
    string SubmittedName,
    string Status,
    string? ResultUrl = null,
    string? ErrorMessage = null);
