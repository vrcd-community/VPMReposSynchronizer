using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using VPMReposSynchronizer.Core.Options;
using VPMReposSynchronizer.Core.Utils;

namespace VPMReposSynchronizer.Core.Services.FileHost;

public class LocalFileHostService(IOptions<LocalFileHostOptions> options) : IFileHostService
{
    private readonly string _filePath = options.Value.FilesPath;

    public async Task<string> UploadFileAsync(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("File to upload is not exits", path);
        }

        await using var fileStream = File.OpenRead(path);

        var fileName = await FileUtils.HashStream(fileStream);
        var filePath = Path.Combine(_filePath, fileName);

        if (!Directory.Exists(_filePath))
        {
            Directory.CreateDirectory(_filePath);
        }

        File.Copy(path, filePath);
        return fileName;
    }

    public Task<string> GetFileUriAsync(string fileId)
    {
        var filePath = Path.Combine(_filePath, fileId).Replace('\\', '/');
        if (!File.Exists(filePath))
        {
            throw new InvalidOperationException("File Id (File Path) Not found",
                new FileNotFoundException("File not found", filePath));
        }

        return Task.FromResult(new Uri(options.Value.BaseUrl, filePath).ToString());
    }

    public async Task<string?> LookupFileByHashAsync(string hash)
    {
        var filePath = Path.Combine(_filePath, hash);

        return File.Exists(filePath) ? hash : null;
    }
}