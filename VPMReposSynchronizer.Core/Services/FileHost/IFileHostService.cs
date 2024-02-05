namespace VPMReposSynchronizer.Core.Services.FileHost;

public interface IFileHostService
{
    public Task<string> UploadFileAsync(string path, string name);
    public Task<string> GetFileUriAsync(string fileId);
    public Task<string?> LookupFileByHashAsync(string hash);
    public Task<bool> IsFileExist(string fileId);
}