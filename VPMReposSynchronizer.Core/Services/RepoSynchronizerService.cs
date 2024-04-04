using System.Text.Json;
using FreeScheduler;
using Microsoft.Extensions.Logging;
using VPMReposSynchronizer.Core.Models.Entity;
using VPMReposSynchronizer.Core.Models.Types;
using VPMReposSynchronizer.Core.Services.FileHost;
using VPMReposSynchronizer.Core.Utils;

namespace VPMReposSynchronizer.Core.Services;

public class RepoSynchronizerService(
    RepoMetaDataService repoMetaDataService,
    RepoSyncTaskService repoSyncTaskService,
    IFileHostService fileHostService,
    ILogger<RepoSynchronizerService> logger,
    IHttpClientFactory httpClientFactory,
    Scheduler scheduler)
{
    public async Task ScheduleAllTasks()
    {
        var repos = await repoMetaDataService.GetAllRepos();

        foreach (var repo in repos)
        {
            scheduler.AddTaskCustom("SyncRepo", repo.Id, repo.SyncTaskCron);
        }
    }

    public async Task StartSync(string repoId)
    {
        var repo = await repoMetaDataService.GetRepoById(repoId);
        if (repo is null)
            throw new InvalidOperationException($"Repo with id {repoId} not found");

        var taskId = await repoSyncTaskService.AddSyncTaskAsync(repoId, "");

        try
        {
            await StartSync(repo.UpStreamUrl, repo.Id);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while Syncing Repo {RepoId}", repoId);
            await repoSyncTaskService.UpdateSyncTaskAsync(taskId, DateTimeOffset.Now, SyncTaskStatus.Failed);
            return;
        }

        await repoSyncTaskService.UpdateSyncTaskAsync(taskId, DateTimeOffset.Now, SyncTaskStatus.Completed);
    }

    public async Task StartSync(string sourceRepoUrl, string sourceRepoId)
    {
        logger.LogInformation("Start Sync with: {SourceRepoId}@{RepoUrl}", sourceRepoId, sourceRepoUrl);

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
            if (package.ZipSha256 is null &&
                await repoMetaDataService.GetVpmPackage(package.Name, package.Version) is { } packageEntity)
            {
                logger.LogWarning(
                    "Package {PackageName}@{PackageVersion}@{SourceRepoId} have not ZipSha256 and the package is already downloaded & uploaded before, so we skip download this package",
                    package.Name,
                    package.Version,
                    sourceRepoId);

                await repoMetaDataService.AddOrUpdateVpmPackageAsync(package, packageEntity.FileId, sourceRepoId,
                    repo.Id ?? sourceRepoId);
                logger.LogInformation("Add {PackageName}@{PackageVersion} to DataBase", package.Name, package.Version);

                continue;
            }

            var fileId = "";
            if (package.ZipSha256 is { } sha256 && await fileHostService.LookupFileByHashAsync(sha256) is
                    { } tempFileId)
            {
                logger.LogInformation(
                    "File is already Downloaded & Uploaded, Skip Download {PackageName}@{PackageVersion}@{SourceRepoId}",
                    package.Name,
                    package.Version,
                    sourceRepoId);
                fileId = tempFileId;
            }

            if (fileId == "")
            {
                logger.LogInformation("Start Downloading {PackageName}@{PackageVersion}@{SourceRepoId}: {PackageUrl}",
                    package.Name,
                    package.Version, sourceRepoId, package.Url);

                var tempFileName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                await using var stream = await httpClient.GetStreamAsync(package.Url);
                await using var tempFileStream = File.Create(tempFileName);
                await stream.CopyToAsync(tempFileStream);

                tempFileStream.Close();

                logger.LogInformation("Downloaded {PackageName}@{PackageVersion}@{SourceRepoId}", package.Name,
                    package.Version, sourceRepoId);

                var fileHash = await FileUtils.HashFile(tempFileName);
                if (await fileHostService.LookupFileByHashAsync(fileHash) is not { } fileRecordId)
                {
                    logger.LogInformation(
                        "Uploading {PackageName}@{PackageVersion}@{SourceRepoId} to File Host Service", package.Name,
                        package.Version, sourceRepoId);
                    var fileName = Path.GetFileName(new Uri(package.Url).AbsolutePath);
                    fileId = await fileHostService.UploadFileAsync(tempFileName, fileName);
                    logger.LogInformation("Uploaded {PackageName}@{PackageVersion}@{SourceRepoId} to File Host Service",
                        package.Name,
                        package.Version, sourceRepoId);
                }
                else
                {
                    logger.LogInformation(
                        "File is already Uploaded, Skip Upload {PackageName}@{PackageVersion}@{SourceRepoId}",
                        package.Name,
                        package.Version,
                        sourceRepoId);

                    fileId = fileRecordId;
                }

                File.Delete(tempFileName);
            }

            await repoMetaDataService.AddOrUpdateVpmPackageAsync(package, fileId, sourceRepoId,
                repo.Id ?? sourceRepoId);
            logger.LogInformation("Add {PackageName}@{PackageVersion} to DataBase", package.Name, package.Version);
        }
    }
}