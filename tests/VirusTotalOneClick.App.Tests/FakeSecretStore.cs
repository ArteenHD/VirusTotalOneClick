using VirusTotalOneClick.Core;

namespace VirusTotalOneClick.AppTests;

internal sealed class FakeSecretStore : ISecretStore
{
    private string? _apiKey;

    public bool ThrowOnSave { get; set; }

    public bool ThrowOnDelete { get; set; }

    public void SaveApiKey(string apiKey)
    {
        if (ThrowOnSave)
        {
            throw new IOException("secret store unavailable");
        }

        _apiKey = apiKey;
    }

    public string? ReadApiKey() => _apiKey;

    public void DeleteApiKey()
    {
        if (ThrowOnDelete)
        {
            throw new IOException("apikey.bin is in use by another process");
        }

        _apiKey = null;
    }
}
