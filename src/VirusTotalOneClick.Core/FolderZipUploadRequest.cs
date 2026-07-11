namespace VirusTotalOneClick.Core;

public sealed record FolderZipUploadRequest(
    string SourceRootPath,
    IReadOnlyList<string> SelectedFiles);
