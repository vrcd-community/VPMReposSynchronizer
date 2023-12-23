using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VPMReposSynchronizer.Core.Options;
using VPMReposSynchronizer.Core.Utils;

namespace VPMReposSynchronizer.Core.Services.FileHost;

public class LocalFileHostService(IOptions<LocalFileHostOptions> options, ILogger<LocalFileHostService> logger) : IFileHostService
{
    private readonly string _filePath = options.Value.FilesPath;

    public async Task<string> UploadFileAsync(string path)
    {
        if (!File.Exists(path))
        {
            var exception = new FileNotFoundException("File to upload is not exits", path);
            logger.LogError(exception, "File {Path} to upload is not exits", path);
        }

        logger.LogInformation("Hashing File {Path} to prepare for storage it in local file system", path);

        await using var fileStream = File.OpenRead(path);

        var fileName = await FileUtils.HashStream(fileStream);
        var filePath = Path.Combine(_filePath, fileName);

        logger.LogInformation("File {Path} hash is {FileHash}", path, fileName);

        if (!Directory.Exists(_filePath))
        {
            logger.LogInformation("Creating directory {Path} to store files", _filePath);
            Directory.CreateDirectory(_filePath);
        }

        File.Copy(path, filePath);
        logger.LogInformation("File {FileHash} ({Path}) copied to {FilePath}", fileName, path, filePath);

        return fileName;
    }

    public Task<string> GetFileUriAsync(string fileId)
    {
        var filePath = Path.Combine(_filePath, fileId).Replace('\\', '/');

        if (File.Exists(filePath))
            return Task.FromResult(new Uri(options.Value.BaseUrl, filePath).ToString());

        var exception = new InvalidOperationException("File Id (File Path) Not found",
            new FileNotFoundException("File not found", filePath));

        logger.LogError(exception, "File {Id} ({FilePath}) Not found", fileId, filePath);
        throw exception;

    }

    public Task<string?> LookupFileByHashAsync(string hash)
    {
        var filePath = Path.Combine(_filePath, hash);

        return Task.FromResult(File.Exists(filePath) ? hash : null);
    }
}