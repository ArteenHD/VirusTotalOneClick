namespace VirusTotalOneClick.Core;

/// <summary>One launch's worth of selected paths, handed between instances.</summary>
public sealed record CoalescedInvocation(IReadOnlyList<string> Paths);
