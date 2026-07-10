namespace VirusTotalOneClick.Core;

public sealed record UploadPlan(
    IReadOnlyList<FileSelection> Selections,
    bool CanUploadImmediately,
    bool RequiresFolderReview);
