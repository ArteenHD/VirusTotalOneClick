using System.Globalization;
using VirusTotalOneClick.Core;

namespace VirusTotalOneClick.UI;

public sealed class HistoryRowViewModel
{
    public HistoryRowViewModel(UploadHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Entry = entry;
    }

    public UploadHistoryEntry Entry { get; }

    public string OriginalPath => Entry.OriginalPath;

    public string SubmittedName => Entry.SubmittedName;

    public string Sha256 => Entry.Sha256;

    public string Sha256Display => Entry.Sha256.Length > 12 ? Entry.Sha256[..12] + "..." : Entry.Sha256;

    public string Mode => Entry.Mode;

    public string Status => Entry.Status;

    public string StatusLabel => Entry.Status.ToLowerInvariant() switch
    {
        "uploaded" or "completed" => "Completed",
        "uploading" => "Uploading",
        "waiting" or "queued" => "Queued",
        "failed" => "Failed",
        _ => Entry.Status,
    };

    public string ResultUrl => Entry.ResultUrl ?? string.Empty;

    public string ResultLabel => string.IsNullOrEmpty(Entry.ResultUrl) ? string.Empty : "Open";

    public DateTimeOffset UploadedAtUtc => Entry.UploadedAtUtc;

    public string DateDisplay => FormatDate(Entry.UploadedAtUtc, DateTimeOffset.Now);

    public bool MatchesSearch(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return Contains(OriginalPath, query)
            || Contains(SubmittedName, query)
            || Contains(Sha256, query)
            || Contains(Mode, query)
            || Contains(Status, query)
            || Contains(ResultUrl, query);
    }

    internal static string FormatDate(DateTimeOffset uploadedAt, DateTimeOffset now)
    {
        var local = uploadedAt.ToLocalTime();
        var nowLocal = now.ToLocalTime();
        if (local.Date == nowLocal.Date)
        {
            var delta = nowLocal - local;
            if (delta.TotalSeconds < 60)
            {
                return "just now";
            }

            if (delta.TotalMinutes < 60)
            {
                var minutes = (int)Math.Max(1, delta.TotalMinutes);
                return $"{minutes} minute{(minutes == 1 ? string.Empty : "s")} ago";
            }

            var hours = (int)Math.Max(1, delta.TotalHours);
            return $"{hours} hour{(hours == 1 ? string.Empty : "s")} ago";
        }

        if (local.Date == nowLocal.Date.AddDays(-1))
        {
            return "Yesterday";
        }

        return local.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
    }

    private static bool Contains(string? source, string query) =>
        !string.IsNullOrEmpty(source) && source.Contains(query, StringComparison.OrdinalIgnoreCase);
}
