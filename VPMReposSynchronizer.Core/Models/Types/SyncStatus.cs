﻿namespace VPMReposSynchronizer.Core.Models.Types;

public record SyncStatus(
    DateTimeOffset? SyncStarted,
    DateTimeOffset? SyncEnded,
    SyncStatusType Status,
    string Message = "");

public record SyncStatusPublic(
    DateTimeOffset? SyncStarted,
    DateTimeOffset? SyncEnded,
    SyncStatusType Status,
    string Url,
    string Message = "")
{
    public SyncStatusPublic(string Url, SyncStatus syncStatus) : this(
        syncStatus.SyncStarted,
        syncStatus.SyncEnded,
        syncStatus.Status,
        Url,
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