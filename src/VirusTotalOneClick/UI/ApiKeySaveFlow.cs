using VirusTotalOneClick.Core;

namespace VirusTotalOneClick.UI;

public sealed record ApiKeySaveOutcome(bool Saved, string StatusText);

/// <summary>
/// The one API-key save pipeline — trim, test-key policy, 64-hex format check,
/// optional live probe, then store write — so every place a key can be entered
/// enforces the exact same rules and wording.
/// </summary>
public static class ApiKeySaveFlow
{
    public static async Task<ApiKeySaveOutcome> SaveAsync(
        string apiKeyText,
        ISecretStore? secretStore,
        ApiKeyValidator validator,
        Func<string, CancellationToken, Task<ApiKeyValidationStatus>>? validateApiKey,
        Func<string, Task>? onChecking,
        CancellationToken cancellationToken)
    {
        if (secretStore is null)
        {
            return new ApiKeySaveOutcome(false, "API key cannot be saved in this session.");
        }

        var key = (apiKeyText ?? string.Empty).Trim();

        if (validator.IsTestDevKey(key))
        {
            return SaveToStore(secretStore, key, "API key saved.");
        }

        if (!ApiKeyValidator.IsValidFormat(key))
        {
            return new ApiKeySaveOutcome(false, "API key must be 64 hex characters.");
        }

        if (validateApiKey is null)
        {
            return SaveToStore(secretStore, key, "API key saved.");
        }

        ApiKeyValidationStatus status;
        try
        {
            if (onChecking is not null)
            {
                await onChecking("Checking VirusTotal API key...").ConfigureAwait(true);
            }

            status = await validateApiKey(key, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ApiKeySaveOutcome(false, "API key check canceled. Key was not saved.");
        }
        catch (Exception)
        {
            // Includes an un-canceled TaskCanceledException from the validation
            // HttpClient's own timeout — that is a network failure, not a
            // cancellation the caller asked for, so it takes the same
            // optimistic-save path as any other unreachable-VirusTotal case.
            status = ApiKeyValidationStatus.NetworkError;
        }

        return status switch
        {
            ApiKeyValidationStatus.Rejected => new ApiKeySaveOutcome(
                false, "VirusTotal rejected this key. Check that you copied the full key."),
            ApiKeyValidationStatus.NetworkError => SaveToStore(
                secretStore,
                key,
                "Could not reach VirusTotal to validate. Key saved and will be verified on first upload."),
            _ => SaveToStore(secretStore, key, "API key saved."),
        };
    }

    private static ApiKeySaveOutcome SaveToStore(ISecretStore secretStore, string key, string successStatusText)
    {
        try
        {
            secretStore.SaveApiKey(key);
            return new ApiKeySaveOutcome(true, successStatusText);
        }
        catch (Exception ex)
        {
            return new ApiKeySaveOutcome(false, $"Could not save API key: {ex.GetBaseException().Message}");
        }
    }
}
