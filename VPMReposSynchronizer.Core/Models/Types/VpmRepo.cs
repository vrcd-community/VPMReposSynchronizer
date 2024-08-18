namespace VPMReposSynchronizer.Core.Models.Types;

// See https://vcc.docs.vrchat.com/vpm/packages#vpm-manifest-additions and https://vcc.docs.vrchat.com/vpm/repos for more details.

public record VpmRepo(
    string Name,
    string Author,
    string Url,
    string? Id, // For some mother f****k repo which don't have a id
    Dictionary<string, VpmRepoPackageVersions> Packages
);

public record VpmRepoPackageVersions(
    Dictionary<string, VpmPackage> Versions
);