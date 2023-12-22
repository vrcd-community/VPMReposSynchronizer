namespace VPMReposSynchronizer.Core.Options;

public class S3FileHostServiceOptions
{
    public string AccessKey { get; set; }
    public string SecretKey { get; set; }
    public string EndPointUrl { get; set; }
    public string BucketName { get; set; }

    public string FileKeyPrefix { get; set; }
    public string FileKeySuffix { get; set; }

    public bool EnablePublicAccess { get; set; }
    public double PreSignedURLExpires { get; set; } = 1800;
    public Uri CdnUrl { get; set; }
}