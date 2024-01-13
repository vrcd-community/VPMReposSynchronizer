namespace VPMReposSynchronizer.Core.Models.Types;

public record SyncStatus(
    DateTimeOffset? SyncStarted,
    DateTimeOffset? SyncEnded,
    string Url,
    SyncStatusType Status,
    string Message = "");

public record SyncStatusPublic(
    DateTimeOffset? SyncStarted,
    DateTimeOffset? SyncEnded,
    SyncStatusType Status,
    string Id,
    string Url,
    string Message = "")
{
    public SyncStatusPublic(string Id, SyncStatus syncStatus) : this(
        syncStatus.SyncStarted,
        syncStatus.SyncEnded,
        syncStatus.Status,
        Id,
        syncStatus.Url,
        syncStatus.Message)
    {
    }
}

public enum SyncStatusType
{
    Success,
    Syncing,
    Failed,
    Never
}