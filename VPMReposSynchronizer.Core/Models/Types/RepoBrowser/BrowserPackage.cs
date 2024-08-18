namespace VPMReposSynchronizer.Core.Models.Types.RepoBrowser;

public record BrowserPackage(
    VpmPackage Latest,
    VpmPackage[] Versions,
    string RepoId,
    string RepoUrl
);