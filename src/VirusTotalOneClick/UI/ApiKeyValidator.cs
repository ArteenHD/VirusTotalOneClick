using System.Text.RegularExpressions;

namespace VirusTotalOneClick.UI;

/// <summary>
/// API-key checks shared by Setup and Settings. The development test key is
/// honored only when the composition root injects an allowing policy, so both
/// behaviors stay observable at runtime.
/// </summary>
public sealed partial class ApiKeyValidator
{
    public const string TestDevKey = "testdev";

    private readonly bool _allowTestKey;

    public ApiKeyValidator(bool allowTestKey)
    {
        _allowTestKey = allowTestKey;
    }

    public bool IsTestDevKey(string? apiKey) =>
        _allowTestKey && string.Equals(apiKey, TestDevKey, StringComparison.Ordinal);

    public static bool IsValidFormat(string apiKey) => Hex64Regex().IsMatch(apiKey);

    [GeneratedRegex(@"\A[0-9a-fA-F]{64}\z")]
    private static partial Regex Hex64Regex();
}
