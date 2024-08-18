using System.ComponentModel.DataAnnotations;

namespace VPMReposSynchronizer.Core.Models.Entity;

public class VpmPackageEntity
{
    [Key] [MaxLength(500)] public required string PackageId { get; set; } // e.g com.vrchat.worlds@3.5.0

    [Required] [MaxLength(100)] public required string UpstreamId { get; set; }

    [Required] [MaxLength(500)] public required string Name { get; set; }
    [MaxLength(500)] public string? DisplayName { get; set; }
    [Required] [MaxLength(50)] public required string Version { get; set; }

    [MaxLength(10000)] public string? UnityVersion { get; set; }
    [MaxLength(10000)] public string? UnityRelease { get; set; }
    [MaxLength(10000)] public string? Description { get; set; }

    [Required] [MaxLength(500)] public required string FileId { get; set; }

    [MaxLength(500)] public string? LocalPath { get; set; }

    [MaxLength(500)] public string? AuthorName { get; set; }
    [MaxLength(500)] public string? AuthorUrl { get; set; }
    [MaxLength(500)] public string? AuthorEmail { get; set; }
    [MaxLength(500)] public string? ZipSha256 { get; set; }

    [MaxLength(10000)] public string? LegacyPackages { get; set; }
    [MaxLength(10000)] public string? LegacyFolders { get; set; }
    [MaxLength(10000)] public string? LegacyFiles { get; set; }

    [MaxLength(10000)] public string? Samples { get; set; }
    [MaxLength(100)] public string? License { get; set; }

    [MaxLength(10000)] public string? Dependencies { get; set; }
    [MaxLength(10000)] public string? GitDependencies { get; set; }
    [MaxLength(10000)] public string? VpmDependencies { get; set; }
    [MaxLength(500)] public string? ChangeLogUrl { get; set; }
    [MaxLength(100)] public string? Type { get; set; }

    public bool? HideInEditor { get; set; }

    [MaxLength(500)] public string? Keywords { get; set; }

    // From VPM-Core-Lib, I don't know what dose these property mean.
    [MaxLength(10000)] public string? Headers { get; set; }
    [MaxLength(10000)] public string? VpmId { get; set; }
}
