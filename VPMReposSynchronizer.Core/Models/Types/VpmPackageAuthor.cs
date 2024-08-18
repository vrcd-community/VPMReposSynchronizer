namespace VPMReposSynchronizer.Core.Models.Types;

// See https://vcc.docs.vrchat.com/vpm/packages#vpm-manifest-additions and https://vcc.docs.vrchat.com/vpm/repos for more details.

public class VpmPackageAuthor
{
    public string Name { get; set; } = "";
    public string? Email { get; set; }
    public string? Url { get; set; }
}