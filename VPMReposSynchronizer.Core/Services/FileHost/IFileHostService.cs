namespace VPMReposSynchronizer.Core.Services.FileHost;

public interface IFileHostService
{
    public Task<string> UploadFileAsync(string path);
    public Task<string> GetFileUriAsync(string fileId);
    public Task<string?> LookupFileByHashAsync(string hash);
}