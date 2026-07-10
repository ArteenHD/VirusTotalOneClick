namespace VirusTotalOneClick.Core;

public static class UploadPlanner
{
    public static UploadPlan Plan(IReadOnlyList<FileSelection> selections, bool confirmFiles)
    {
        var hasFolder = selections.Any(selection => selection.IsDirectory);
        var canUploadImmediately = selections.Count > 0 && !hasFolder && !confirmFiles;

        return new UploadPlan(
            selections,
            CanUploadImmediately: canUploadImmediately,
            RequiresFolderReview: hasFolder);
    }
}
