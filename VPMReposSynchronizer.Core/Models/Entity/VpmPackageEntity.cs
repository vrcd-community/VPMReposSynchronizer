using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace VPMReposSynchronizer.Core.Models.Entity;

public class VpmPackageEntity
{
    [Key]
    public string PackageId { get; set; } // e.g com.vrchat.worlds@3.5.0

    [Required]
    public string Name { get; set; }
    public string? DisplayName { get; set; }
    [Required]
    public string Version { get; set; }

    public string? UnityVersion { get; set; }

    public string? UnityRelease { get; set; }

    public string? Description { get; set; }

    [Required]
    public string FileId { get; set; }

    public string? LocalPath { get; set; }

    public string? AuthorName { get; set; }
    public string? AuthorUrl { get; set; }
    public string? AuthorEmail { get; set; }

    public string? ZipSha256 { get; set; }

    public string? LegacyPackages { get; set; }
    public string? LegacyFolders { get; set; }
    public string? LegacyFiles { get; set; }

    public string? Smaples { get; set; }

    public string? License { get; set; }

    public string? Dependencies { get; set; }
    public string? GitDependencies { get; set; }
    public string? VpmDependencies { get; set; }

    public string? ChangeLogUrl { get; set; }

    public string? Type { get; set; }

    public bool? HideInEditor { get; set; }

    public string? Keywords { get; set; }

    // From VPM-Core-Lib, I don't know what dose these property mean.
    public string? Headers { get; set; }
    public string? VpmId { get; set; }
}