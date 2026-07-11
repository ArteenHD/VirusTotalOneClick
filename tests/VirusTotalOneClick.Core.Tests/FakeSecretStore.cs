namespace VirusTotalOneClick.Core.Tests;

internal sealed class FakeSecretStore(string? apiKey) : ISecretStore
{
    private string? _apiKey = apiKey;

    public void SaveApiKey(string apiKey) => _apiKey = apiKey;

    public string? ReadApiKey() => _apiKey;

    public void DeleteApiKey() => _apiKey = null;
}
