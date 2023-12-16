namespace VPMReposSynchronizer.Core.Options;

public class FileHostServiceOptions
{
    public FileHostServiceType FileHostServiceType { get; set; } = FileHostServiceType.LocalFileHost;
}

public enum FileHostServiceType
{
    LocalFileHost,
    S3FileHost
}