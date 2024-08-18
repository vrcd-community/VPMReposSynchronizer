using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VPMReposSynchronizer.Core.Options;
using VPMReposSynchronizer.Core.Utils;

namespace VPMReposSynchronizer.Core.Services.FileHost;

public class LocalFileHostService(
    IOptions<LocalFileHostOptions> options,
    IOptions<FileHostServiceOptions> fileHostOptions,
    ILogger<LocalFileHostService> logger) : IFileHostService
{
    private readonly string _filePath = options.Value.FilesPath;

    public async Task<string> UploadFileAsync(string path, string name)
    {
        if (!File.Exists(path))
        {
            var exception = new FileNotFoundException("File to upload is not exits", path);
            logger.LogError(exception, "File {Path} to upload is not exits", path);
        }

        logger.LogInformation("Hashing File {Path} to prepare for storage it in local file system", path);

        await using var fileStream = File.OpenRead(path);

        var fileHash = await FileUtils.HashStream(fileStream);
        var filePath = Path.Combine(_filePath, fileHash, name);
        var fileHashPath = Path.Combine(_filePath, fileHash);

        logger.LogInformation("File {Path} hash is {FileHash}", path, fileHash);

        if (!Directory.Exists(_filePath))
        {
            logger.LogInformation("Creating directory {Path} to store files", _filePath);
            Directory.CreateDirectory(_filePath);
        }

        if (!Directory.Exists(fileHashPath)) Directory.CreateDirectory(fileHashPath);

        File.Copy(path, filePath);
        logger.LogInformation("File {FileHash} ({Path}) copied to {FilePath}", fileHash, path, filePath);

        return fileHash;
    }

    public Task<string> GetFileUriAsync(string fileId)
    {
        var filePath = GetFilePath(fileId);

        if (filePath is not null && File.Exists(filePath))
            return Task.FromResult(new Uri(fileHostOptions.Value.BaseUrl, filePath).ToString());

        var exception = new InvalidOperationException("File Id (File Path) Not found",
            new FileNotFoundException("File not found", filePath));

        logger.LogError(exception, "File {Id} ({FilePath}) Not found", fileId, filePath);
        throw exception;
    }

    public Task<string?> LookupFileByHashAsync(string hash)
    {
        var filePath = GetFilePath(hash);

        if (filePath is null) return Task.FromResult<string?>(null);

        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (File.Exists(filePath)) return Task.FromResult<string?>(hash);

        return Task.FromResult<string?>(null);
    }

    public Task<bool> IsFileExist(string fileId)
    {
        var filePath = GetFilePath(fileId);

        return Task.FromResult(File.Exists(filePath));
    }

    private string? GetFilePath(string hash)
    {
        var hashPath = Path.Combine(_filePath, hash);
        if (!Directory.Exists(Path.Combine(_filePath, hash))) return null;

        var files = Directory.GetFiles(hashPath);

        return files.Length != 0 ? files[0] : null;
    }
}