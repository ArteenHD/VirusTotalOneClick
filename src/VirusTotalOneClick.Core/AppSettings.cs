namespace VirusTotalOneClick.Core;

public sealed record AppSettings
{
    public static AppSettings Default { get; } = new();

    public bool ConfirmBeforeFileUpload { get; init; }

    public FolderUploadMode DefaultFolderMode { get; init; } = FolderUploadMode.ReviewSelectedFiles;

    public bool KeepLocalHistory { get; init; } = true;

    public bool ScrubSubmittedFilename { get; init; }

    public bool ScrubZipInternalPaths { get; init; } = true;

    public bool EnableClassicContextMenu { get; init; } = true;

    public bool EnableSendToShortcut { get; init; } = true;

    public bool EnableStartMenuShortcuts { get; init; } = true;

    public bool LaunchOnStartup { get; init; }
}
