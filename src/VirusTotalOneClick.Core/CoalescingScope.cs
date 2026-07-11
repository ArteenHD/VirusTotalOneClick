namespace VirusTotalOneClick.Core;

public static class CoalescingScope
{
    /// <summary>
    /// Only interactive upload launches share an instance. Shell command verbs
    /// and the settings/history windows always act standalone.
    /// </summary>
    public static bool AppliesTo(AppMode mode) => mode == AppMode.Upload;
}
