using VPMReposSynchronizer.Core.Models.Entity;

namespace VPMReposSynchronizer.Core.Models.Types;

public record SyncStatusPublic(
    DateTimeOffset? SyncStarted,
    DateTimeOffset? SyncEnded,
    SyncTaskStatus Status,
    long SyncTaskId,
    string RepoId,
    string RepoUpstreamUrl,
    string Message = "");