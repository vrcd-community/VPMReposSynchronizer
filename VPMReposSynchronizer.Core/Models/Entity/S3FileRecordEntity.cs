using System.ComponentModel.DataAnnotations;

namespace VPMReposSynchronizer.Core.Models.Entity;

public class S3FileRecordEntity
{
    [Key]
    public string FileKey { get; set; }
    [Required]
    public string FileHash { get; set; }
}