namespace VirusTotalOneClick.Core;

public sealed record FolderScanResult(IReadOnlyList<string> Files, bool Truncated, IReadOnlyList<string> Errors);
