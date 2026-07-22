using System.Security.Cryptography;
using System.Text;

namespace VirusTotalOneClick.Core;

/// <summary>
/// Stores the VirusTotal API key in a DPAPI-protected file scoped to the current
/// Windows user. DPAPI protects the key at rest against other users and offline
/// theft; it does not protect against code already running as the same user.
/// </summary>
public sealed class DpapiFileSecretStore : ISecretStore
{
    private readonly string _path;

    public DpapiFileSecretStore(string appDataDirectory)
    {
        _path = Path.Combine(appDataDirectory, "apikey.bin");
    }

    public void SaveApiKey(string apiKey)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var bytes = Encoding.UTF8.GetBytes(apiKey);
        var protectedBytes = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_path, protectedBytes);
    }

    public string? ReadApiKey()
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        try
        {
            var protectedBytes = File.ReadAllBytes(_path);
            var bytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (CryptographicException)
        {
            // The blob is genuinely undecryptable (corrupt, or protected under a
            // different Windows profile), so there is no key to recover.
            TryRemoveUnreadableBlob();
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A transient I/O failure (e.g. the file is locked by a scanner or
            // backup tool) is not evidence of corruption, so the stored key is
            // left untouched for the next read attempt.
            return null;
        }
    }

    public void DeleteApiKey()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    private void TryRemoveUnreadableBlob()
    {
        try
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
