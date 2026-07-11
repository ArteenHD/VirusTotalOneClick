namespace VirusTotalOneClick.Core;

public enum FileUploadWorkflowStatus
{
    Completed,
    CompletedWithFailures,
    Canceled,
    Failed,
    MissingApiKey,
    NoFiles,
}
