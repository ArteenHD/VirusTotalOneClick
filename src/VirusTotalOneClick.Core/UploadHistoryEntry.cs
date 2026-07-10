namespace VirusTotalOneClick.Core;

public sealed record UploadHistoryEntry(
    string OriginalPath,
    string SubmittedName,
    string Sha256,
    DateTimeOffset UploadedAtUtc,
    string Mode,
    string Status,
    string? ResultUrl);
