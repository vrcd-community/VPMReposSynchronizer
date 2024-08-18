using System.ComponentModel.DataAnnotations;

namespace VPMReposSynchronizer.Core.Models.Entity;

public class VpmRepoEntity
{
    [Key] [MaxLength(500)] public required string Id { get; set; }

    [Required] [MaxLength(500)] public required string RepoId { get; set; }

    [MaxLength(500)] public required string Name { get; set; }

    [MaxLength(500)] public required string Author { get; set; }

    [MaxLength(1000)] public required string Description { get; set; }

    [MaxLength(1000)] public required string UpStreamUrl { get; set; }

    [Required] [MaxLength(50)] public required string SyncTaskCron { get; set; }
}