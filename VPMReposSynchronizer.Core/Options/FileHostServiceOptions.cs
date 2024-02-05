namespace VPMReposSynchronizer.Core.Options;

public class FileHostServiceOptions
{
    public FileHostServiceType FileHostServiceType { get; set; } = FileHostServiceType.LocalFileHost;
    public Uri BaseUrl { get; set; } = new("https://example.com/");
}

public enum FileHostServiceType
{
    LocalFileHost,
    S3FileHost
}