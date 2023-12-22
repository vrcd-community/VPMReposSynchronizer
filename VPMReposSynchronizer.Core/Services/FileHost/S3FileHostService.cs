using System.Net;
using Amazon.Runtime.Endpoints;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using VPMReposSynchronizer.Core.Options;
using VPMReposSynchronizer.Core.Utils;

namespace VPMReposSynchronizer.Core.Services.FileHost;

public class S3FileHostService : IFileHostService
{
    private readonly IOptions<S3FileHostServiceOptions> _options;
    private readonly AmazonS3Client _client;

    public S3FileHostService(IOptions<S3FileHostServiceOptions> options)
    {
        _options = options;

        _client = new AmazonS3Client(options.Value.AccessKey, options.Value.SecretKey, new AmazonS3Config
        {
            ServiceURL = _options.Value.EndPointUrl
        });
    }


    public async Task<string> UploadFileAsync(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("File to upload is not exits", path);
        }

        await using var fileStream = File.OpenRead(path);
        var fileName = await FileUtils.HashStream(fileStream);
        var key = GetFileKey(fileName);

        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _options.Value.BucketName,
            Key = key,
            InputStream = fileStream,
            ContentType = "application/octet-stream"
        });

        return key;
    }

    public async Task<string> GetFileUriAsync(string fileId)
    {
        var key = GetFileKey(fileId);
        if (_options.Value.EnablePublicAccess)
        {
            return new Uri(_options.Value.CdnUrl, key).ToString();
        }

        return await _client.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = _options.Value.BucketName,
            Key = key,
            Expires = DateTime.UtcNow.AddSeconds(_options.Value.PreSignedURLExpires)
        });
    }

    public async Task<string?> LookupFileByHashAsync(string hash)
    {
        var key = GetFileKey(hash);

        try
        {
            await _client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = _options.Value.BucketName,
                Key = key
            });
        }
        catch (AmazonS3Exception e)
        {
            return null;
        }

        return hash;
    }

    private string GetFileKey(string inputKey)
    {
        return _options.Value.FileKeyPrefix + inputKey + _options.Value.FileKeySuffix;
    }
}