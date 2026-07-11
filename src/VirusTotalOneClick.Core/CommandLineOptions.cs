namespace VirusTotalOneClick.Core;

public sealed record CommandLineOptions(AppMode Mode, IReadOnlyList<string> Paths);
