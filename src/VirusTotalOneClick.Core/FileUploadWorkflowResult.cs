namespace VirusTotalOneClick.Core;

public sealed record FileUploadWorkflowResult(
    FileUploadWorkflowStatus Status,
    int ItemCount,
    int FailedCount = 0,
    IReadOnlyList<FileUploadWorkflowItemResult>? ItemResults = null);
