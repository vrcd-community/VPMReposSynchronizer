namespace VPMReposSynchronizer.Core.Options;

public class FileHostServiceOptions
{
    public FileHostServiceType FileHostServiceType { get; set; } = FileHostServiceType.LocalFileHost;
    public Uri BaseUrl { get; set; } = new("https://example.com/");

    public bool EnableRateLimit { get; set; } = true;
    public double RateLimitWindow { get; set; } = 10000;
    public int RateLimitPerWindow { get; set; } = 5;
}

public enum FileHostServiceType
{
    LocalFileHost,
    S3FileHost
}