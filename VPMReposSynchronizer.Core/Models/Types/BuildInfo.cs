namespace VPMReposSynchronizer.Core.Models.Types;

public record BuildInfo(
    string? Version,
    string Architecture,
    DateTimeOffset BuildDate,
    string CommitHash,
    string BranchName);