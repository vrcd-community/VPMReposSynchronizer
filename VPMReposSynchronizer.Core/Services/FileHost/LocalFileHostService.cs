using System.Security.Cryptography;
using VPMReposSynchronizer.Core.Utils;

namespace VPMReposSynchronizer.Core.Services.FileHost;

public class LocalFileHostService : IFileHostService
{
    private const string FilePath = "files";

    public async Task<string> UploadFileAsync(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("File to upload is not exits", path);
        }

        await using var fileStream = File.OpenRead(path);

        var fileName = await FileUtils.HashStream(fileStream);
        var filePath = Path.Combine(FilePath, fileName);

        if (!Directory.Exists(FilePath))
        {
            Directory.CreateDirectory(FilePath);
        }

        File.Copy(path, filePath);
        return fileName;
    }

    public Task<string> GetFileUriAsync(string fileId)
    {
        var filePath = Path.Combine(FilePath, fileId);
        if (!File.Exists(filePath))
        {
            throw new InvalidOperationException("File Id (File Path) Not found",
                new FileNotFoundException("File not found", filePath));
        }

        return Task.FromResult("http://localhost:5218/" + filePath);
    }

    public async Task<string?> LookupFileByHashAsync(string hash)
    {
        var filePath = Path.Combine(FilePath, hash);

        return File.Exists(filePath) ? hash : null;
    }
}