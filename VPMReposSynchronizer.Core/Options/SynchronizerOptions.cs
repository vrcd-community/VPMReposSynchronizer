namespace VPMReposSynchronizer.Core.Options;

public class SynchronizerOptions
{
    public Dictionary<string, string> SourceRepos { get; set; } = new();
    public long SyncPeriod { get; set; } = 3600;
}