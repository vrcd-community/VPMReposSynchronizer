namespace VPMReposSynchronizer.Core.Options;

public class SynchronizerOptions
{
    public string[] SourceRepoUrls { get; set; } = Array.Empty<string>();
    public long SyncPeriod { get; set; } = 3600;
}