using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VPMReposSynchronizer.Core.DbContexts;
using VPMReposSynchronizer.Core.Models.Entity;
using VPMReposSynchronizer.Core.Options;
using VPMReposSynchronizer.Core.Utils;

namespace VPMReposSynchronizer.Core.Services.FileHost;

public class S3FileHostService : IFileHostService
{
    private readonly IOptions<S3FileHostServiceOptions> _options;
    private readonly AmazonS3Client _client;
    private readonly DefaultDbContext _dbContext;
    private readonly ILogger<S3FileHostService> _logger;

    public S3FileHostService(IOptions<S3FileHostServiceOptions> options, DefaultDbContext dbContext,
        ILogger<S3FileHostService> logger)
    {
        _options = options;
        _dbContext = dbContext;
        _logger = logger;

        _client = new AmazonS3Client(options.Value.AccessKey, options.Value.SecretKey, new AmazonS3Config
        {
            ServiceURL = _options.Value.EndPointUrl
        });
    }


    public async Task<string> UploadFileAsync(string path)
    {
        if (!File.Exists(path))
        {
            var exception = new FileNotFoundException("File to upload is not exits", path);
            _logger.LogError(exception, "File {Path} to upload is not exits", path);
        }

        _logger.LogInformation("Hashing File {Path} to prepare for upload it to S3", path);

        await using var fileStream = File.OpenRead(path);
        var fileHash = await FileUtils.HashStream(fileStream);
        var key = GetFileKey(fileHash);

        _logger.LogInformation("File {Path} hash is {FileHash}, Checking File Record...", path, fileHash);

        var fileRecord = await _dbContext.S3FileRecords.FindAsync(key);
        if (fileRecord is not null)
        {
            _logger.LogWarning(
                "File {FileKey} ({FilePath}) already exists in S3 File Records Database, Checking is file exists in S3...",
                key, path);

            if (await LookupFileByHashAsync(key) is not null)
            {
                _logger.LogWarning("File {FileKey} ({FilePath}) already exists in S3, Skip Upload", key, path);
                return key;
            }

            _logger.LogWarning("File{FileKey} ({FilePath}) already exists in S3 File Records Database, but not in S3, Upload will continue",
                key, path);
        }

        _logger.LogInformation("Uploading File {Path} to S3", path);
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _options.Value.BucketName,
            Key = key,
            InputStream = fileStream,
            ContentType = "application/octet-stream"
        });

        _logger.LogInformation("Uploaded File {Path} / {FileKey} to S3, Creating File Record....",path, key);

        _dbContext.S3FileRecords.Add(new S3FileRecordEntity
        {
            FileKey = key,
            FileHash = fileHash
        });

        _logger.LogInformation("File Record Created for File {FileKey}", key);

        await _dbContext.SaveChangesAsync();

        return key;
    }

    public async Task<string> GetFileUriAsync(string fileId)
    {
        if (_options.Value.EnablePublicAccess)
        {
            return new Uri(_options.Value.CdnUrl, fileId).ToString();
        }

        return await _client.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = _options.Value.BucketName,
            Key = fileId,
            Expires = DateTime.UtcNow.AddSeconds(_options.Value.PreSignedUrlExpires)
        });
    }

    public async Task<string?> LookupFileByHashAsync(string hash)
    {
        var key = GetFileKey(hash);

        var fileRecord = await _dbContext.S3FileRecords.FindAsync(key);
        if (fileRecord is not null)
            return key;

        _logger.LogWarning("File Not Found in Database, Checking S3 for File {FileHash}", key);

        try
        {
            await _client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = _options.Value.BucketName,
                Key = key
            });

            _logger.LogInformation("File Found in S3, Adding to Database {FileHash}", key);
            _dbContext.S3FileRecords.Add(new S3FileRecordEntity
            {
                FileKey = key,
                FileHash = hash
            });

            await _dbContext.SaveChangesAsync();
        }
        catch (AmazonS3Exception)
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