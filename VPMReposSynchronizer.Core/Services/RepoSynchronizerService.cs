using System.Text.Json;
using Microsoft.Extensions.Logging;
using VPMReposSynchronizer.Core.Models.Types;
using VPMReposSynchronizer.Core.Services.FileHost;
using VPMReposSynchronizer.Core.Utils;

namespace VPMReposSynchronizer.Core.Services;

public class RepoSynchronizerService(
    RepoMetaDataService repoMetaDataService,
    IFileHostService fileHostService,
    ILogger<RepoSynchronizerService> logger,
    IHttpClientFactory httpClientFactory)
{
    public async Task StartSync(string sourceRepoUrl)
    {
        logger.LogInformation("Start Sync with: {RepoUrl}", sourceRepoUrl);
        using var httpClient = httpClientFactory.CreateClient("default");

        var repoResponse = await httpClient.GetStringAsync(sourceRepoUrl);
        var repo = JsonSerializer.Deserialize<VpmRepo>(repoResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        if (repo is null)
        {
            throw new InvalidOperationException("Deserialize Repo Response is not valid");
        }

        logger.LogInformation("Found {PackageCount} Packages",
            repo.Packages.SelectMany(package => package.Value.Versions.Select(version => version.Value)).Count());

        foreach (var package in repo.Packages.SelectMany(
                     package => package.Value.Versions.Select(version => version.Value)))
        {
            var fileId = "";
            if (package.ZipSha256 is { } sha256 && await fileHostService.LookupFileByHashAsync(sha256) is { } tempFileId)
            {
                logger.LogInformation("File is already Downloaded & Uploaded, Skip Download {PackageName}@{PackageVersion}",
                    package.Name,
                    package.Version);
                fileId = tempFileId;
            }

            if (fileId == "")
            {
                logger.LogInformation("Start Downloading {PackageName}@{PackageVersion}: {PackageUrl}", package.Name,
                    package.Version, package.Url);

                var tempFileName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                await using var stream = await httpClient.GetStreamAsync(package.Url);
                await using var tempFileStream = File.Create(tempFileName);
                await stream.CopyToAsync(tempFileStream);

                tempFileStream.Close();

                logger.LogInformation("Downloaded {PackageName}@{PackageVersion}", package.Name, package.Version);

                var fileHash = await FileUtils.HashFile(tempFileName);
                if (await fileHostService.LookupFileByHashAsync(fileHash) is null)
                {
                    logger.LogInformation("Uploading {PackageName}@{PackageVersion} to File Host Service", package.Name,
                        package.Version);
                    var fileName = Path.GetFileName(new Uri(package.Url).AbsolutePath);
                    fileId = await fileHostService.UploadFileAsync(tempFileName, fileName);
                    logger.LogInformation("Uploaded {PackageName}@{PackageVersion} to File Host Service", package.Name,
                        package.Version);
                }
                else
                {
                    logger.LogInformation("File is already Uploaded, Skip Upload {PackageName}@{PackageVersion}",
                        package.Name,
                        package.Version);
                }

                File.Delete(tempFileName);
            }

            await repoMetaDataService.AddOrUpdateVpmPackageAsync(package, fileId);
            logger.LogInformation("Add {PackageName}@{PackageVersion} to DataBase", package.Name, package.Version);
        }
    }
}