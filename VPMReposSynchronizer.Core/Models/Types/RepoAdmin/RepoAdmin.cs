namespace VPMReposSynchronizer.Core.Models.Types.RepoAdmin;

public class RepoAdminUpdateDto
{
    public string ApiId { get; set; }
    public string Name { get; set; }
    public string Author { get; set; }
    public string UpstreamUrl { get; set; }
    public string Description { get; set; }
    public string SyncTaskCron { get; set; }
}
