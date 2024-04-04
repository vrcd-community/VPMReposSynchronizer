using VPMReposSynchronizer.Core.Models.Entity;

namespace VPMReposSynchronizer.Core.Models.Types;

public record SyncTaskPublic(long Id, string RepoId, string Logs, SyncTaskStatus Status, DateTimeOffset StartTime, DateTimeOffset? EndTime);