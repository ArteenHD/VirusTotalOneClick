using System.Reflection;

namespace VirusTotalOneClick.UI;

internal static class AppVersionInfo
{
    /// <summary>
    /// The assembly's informational version with any build-metadata suffix
    /// (e.g. "+&lt;commit&gt;") trimmed off, leaving a plain semver string.
    /// </summary>
    public static string GetInformationalVersion(Assembly? assembly = null)
    {
        var raw = (assembly ?? Assembly.GetExecutingAssembly())
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var metadataIndex = raw.IndexOf('+', StringComparison.Ordinal);
        return metadataIndex < 0 ? raw : raw[..metadataIndex];
    }
}
