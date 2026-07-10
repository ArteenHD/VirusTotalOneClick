using System.Security.Cryptography;

namespace VirusTotalOneClick.Core;

public static class AnonymizerService
{
    public static AnonymizedUploadItem PrepareFile(string path, AnonymizerOptions options)
    {
        var submittedName = options.ScrubSubmittedFilename
            ? CreateSubmittedName(path, ComputeShortId(path))
            : Path.GetFileName(path);

        return new AnonymizedUploadItem(
            OriginalPath: path,
            UploadPath: path,
            SubmittedName: submittedName);
    }

    private static string CreateSubmittedName(string originalPath, string shortId)
    {
        var extension = Path.GetExtension(originalPath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".bin";
        }

        return $"sample-{shortId}{extension.ToLowerInvariant()}";
    }

    private static string ComputeShortId(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream))[..8];
    }
}
