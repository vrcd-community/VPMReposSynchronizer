namespace VPMReposSynchronizer.Core.Models.Types.RepoBrowser;

public record BrowserRepo(
    string Name,
    string Author,
    string UpstreamUrl,
    string UpstreamId,
    string RepoUrl,
    string ApiId,
    SyncStatusPublic? SyncStatus
    );