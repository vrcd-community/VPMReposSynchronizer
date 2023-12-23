using System.Text.Json.Serialization;

namespace VPMReposSynchronizer.Core.Models.Types;

// See https://vcc.docs.vrchat.com/vpm/packages#vpm-manifest-additions and https://vcc.docs.vrchat.com/vpm/repos for more details.

public class VpmPackage
{
    public required string Name { get; set; }
    public string? DisplayName { get; set; }
    public required string Version { get; set; }

    [JsonPropertyName("unity")] public string? UnityVersion { get; set; }
    [JsonPropertyName("unityRelease")] public string? UnityRelease { get; set; }

    public string? Description { get; set; }
    public required string Url { get; set; }
    public string? LocalPath { get; set; }

    public VpmPackageAuthor? Author { get; set; }

    [JsonPropertyName("zipSHA256")] public string? ZipSha256 { get; set; }

    public string[]? LegacyPackages { get; set; }
    public Dictionary<string, string>? LegacyFolders { get; set; }
    public Dictionary<string, string>? LegacyFiles { get; set; }

    [JsonPropertyName("changelogUrl")] public string? ChangeLogUrl { get; set; }

    public Dictionary<string, string>? Dependencies { get; set; }
    public Dictionary<string, string>? GitDependencies { get; set; }
    [JsonPropertyName("vpmDependencies")] public Dictionary<string, string>? VpmDependencies { get; set; }

    public bool? HideInEditor { get; set; }
    public string[]? Keywords { get; set; }
    public string? License { get; set; }

    public PackageSample[]? Samples { get; set; }

    // From VPM-Core-Lib, I don't know what dose these property mean.
    public Dictionary<string, string>? Headers { get; set; }
    public string? Id { get; set; }
}