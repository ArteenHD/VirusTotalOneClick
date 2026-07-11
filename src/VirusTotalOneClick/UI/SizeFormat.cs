namespace VirusTotalOneClick.UI;

internal static class SizeFormat
{
    public static string Format(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return FormattableString.Invariant($"{bytes / 1024.0:0.#} KB");
        }

        if (bytes < 1024L * 1024 * 1024)
        {
            return FormattableString.Invariant($"{bytes / (1024.0 * 1024):0.#} MB");
        }

        return FormattableString.Invariant($"{bytes / (1024.0 * 1024 * 1024):0.#} GB");
    }
}
