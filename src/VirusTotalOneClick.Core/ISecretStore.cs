namespace VirusTotalOneClick.Core;

public interface ISecretStore
{
    void SaveApiKey(string apiKey);

    string? ReadApiKey();

    void DeleteApiKey();
}
