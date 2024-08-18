using System.ComponentModel.DataAnnotations;

namespace VPMReposSynchronizer.Core.Models.Entity;

public class S3FileRecordEntity
{
    [Key] [MaxLength(500)] public required string FileKey { get; set; }

    [Required] [MaxLength(500)] public required string FileHash { get; set; }
}