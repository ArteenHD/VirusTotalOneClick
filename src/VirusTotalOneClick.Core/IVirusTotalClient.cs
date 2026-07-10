namespace VirusTotalOneClick.Core;

public interface IVirusTotalClient
{
    Task<VirusTotalUploadResult> UploadFileAsync(
        string filePath,
        string submittedName,
        CancellationToken cancellationToken);

    Task<ApiKeyValidationStatus> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken);
}
