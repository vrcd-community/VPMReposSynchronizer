using VPMReposSynchronizer.Core.Models.Types;

namespace VPMReposSynchronizer.Core.Services;

public class RepoSynchronizerStatusService
{
    public Dictionary<string, SyncStatus> SyncStatus = new();
}