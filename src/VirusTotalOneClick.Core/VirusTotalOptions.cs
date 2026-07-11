namespace VirusTotalOneClick.Core;

public sealed record VirusTotalOptions(string ApiKey, Uri BaseUri)
{
    // VirusTotal's documented upload ceilings: files up to 32 MB may be posted
    // directly to /files; larger files need a one-time upload URL; nothing over
    // 650 MB is accepted (https://docs.virustotal.com/reference/files-scan).
    public const long DefaultMaxFileUploadLimitBytes = 650L * 1024 * 1024;

    public long SmallFileUploadLimitBytes { get; init; } = 32L * 1024 * 1024;

    public long MaxFileUploadLimitBytes { get; init; } = DefaultMaxFileUploadLimitBytes;
}
