using System.ComponentModel.DataAnnotations;

namespace VPMReposSynchronizer.Core.Models.Entity;

public class VpmRepoEntity
{
    [Key] [MaxLength(500)] public required string Id { get; set; }

    [MaxLength(500)] public string? OriginalRepoId { get; set; }

    [MaxLength(500)] public string? Name { get; set; }

    [MaxLength(500)] public string? Author { get; set; }

    [Required] [MaxLength(1000)] public required string Description { get; set; }

    [Required] [MaxLength(1000)] public required string UpStreamUrl { get; set; }

    [Required] [MaxLength(50)] public required string SyncTaskCron { get; set; }
}
