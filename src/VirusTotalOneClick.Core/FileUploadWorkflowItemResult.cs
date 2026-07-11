namespace VirusTotalOneClick.Core;

public sealed record FileUploadWorkflowItemResult(
    string OriginalPath,
    string SubmittedName,
    string Status,
    string? ResultUrl = null,
    string? ErrorMessage = null);
