namespace VirusTotalOneClick.Core;

public sealed record ZipPlan(IReadOnlyList<string> SourceFiles, string DestinationZipPath, bool ScrubInternalPaths);
