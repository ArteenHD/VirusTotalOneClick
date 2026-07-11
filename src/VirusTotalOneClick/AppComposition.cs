using VirusTotalOneClick.Core;

namespace VirusTotalOneClick;

/// <summary>
/// Everything a window needs, assembled once per launch: the resolved launch
/// options plus the seams into settings, secrets, history, shell integration,
/// and the upload workflows.
/// </summary>
public sealed class AppComposition
{
    public required CommandLineOptions Options { get; init; }

    public required AppSettings Settings { get; init; }

    public required Action<AppSettings> SaveSettings { get; init; }

    public required ISecretStore SecretStore { get; init; }

    public required Action ClearHistory { get; init; }

    public required Func<IReadOnlyList<UploadHistoryEntry>> ReadHistory { get; init; }

    public required Action<AppSettings> ApplyIntegration { get; init; }

    public required Func<IReadOnlyList<string>, Func<CancellationToken, Task<FileUploadWorkflowResult>>> CreateUploadAction { get; init; }

    public required Func<FolderZipUploadRequest, Func<CancellationToken, Task<FileUploadWorkflowResult>>> CreateZipUploadAction { get; init; }

    public required Func<string, CancellationToken, Task<ApiKeyValidationStatus>> ValidateApiKey { get; init; }
}
