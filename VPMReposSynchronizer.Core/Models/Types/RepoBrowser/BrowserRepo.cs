namespace VPMReposSynchronizer.Core.Models.Types.RepoBrowser;

public class BrowserRepo
{
    public string Name { get; set; }
    public string Author { get; set; }
    public string UpstreamUrl { get; set; }
    public string UpstreamId { get; set; }
    public string RepoUrl { get; set; }
    public string ApiId { get; set; }
    public string Description { get; set; }
    public string SyncTaskCron { get; set; }
    public SyncStatusPublic SyncStatus { get; set; }
}