namespace VPMReposSynchronizer.Core.Services.RepoSync;

/// <summary>
/// Raised when a single package version cannot be mirrored because the downloaded
/// archive does not match the hash declared by the upstream manifest.
/// The existing mirror record must be kept and the remaining versions must still sync.
/// </summary>
public class PackageSyncException(string message) : Exception(message)
{
}
