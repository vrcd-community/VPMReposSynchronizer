using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace VPMReposSynchronizer.Core.Models.Entity;

[Index(nameof(Id), nameof(RepoId), nameof(Status))]
public class SyncTaskEntity
{
    [Key] public long Id { get; set; }

    [Required] public required string RepoId { get; set; }

    [Required] public required string LogPath { get; set; }

    [Required] public required SyncTaskStatus Status { get; set; }

    [Required] public DateTimeOffset StartTime { get; set; }

    public DateTimeOffset? EndTime { get; set; }
}

public enum SyncTaskStatus
{
    Running,
    Completed,
    Failed,
    Interrupted,
    Pending
}
