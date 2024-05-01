using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using Serilog.Sinks.ILogger;
using Serilog.Templates;
using VPMReposSynchronizer.Core.DbContexts;
using VPMReposSynchronizer.Core.Models.Entity;
using VPMReposSynchronizer.Core.Models.Types;
using VPMReposSynchronizer.Core.Services.FileHost;
using VPMReposSynchronizer.Core.Utils;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace VPMReposSynchronizer.Core.Services.RepoSync;

public class RepoSynchronizerService(
    RepoMetaDataService repoMetaDataService,
    RepoSyncTaskService repoSyncTaskService,
    IFileHostService fileHostService,
    ILogger<RepoSynchronizerService> logger,
    HttpClient httpClient,
    DefaultDbContext defaultDbContext)
{
    const string logTemplate =
        "[{@t:yyyy-MM-dd HH:mm:ss} " +
        "{@l:u3}]" +
        "{#if SourceContext is not null} [{Substring(SourceContext, LastIndexOf(SourceContext, '.') + 1)}]{#end}" +
        "{#if @p.Scope is not null} [{#each s in Scope}{s}{#delimit} {#end}]{#end}" +
        " {@m}" +
        "\n{@x}";

    public async Task StartSync(string repoId)
    {
        var repo = await repoMetaDataService.GetRepoById(repoId);
        if (repo is null)
        {
            throw new InvalidOperationException($"Repo with id {repoId} not found");
        }

        var taskId = await repoSyncTaskService.AddSyncTaskAsync(repoId, "");

        var logPath = Path.GetFullPath(Path.Combine("sync-tasks-logs",
            $"syncTask-{taskId}-{repoId}-{DateTimeOffset.Now:yyyy-MM-dd-HH-mm-ss}.log"));
        var taskLogger = GetTaskLogger(logger, logPath);

        await repoSyncTaskService.UpdateSyncTaskAsync(taskId, logPath);

        using (taskLogger.BeginScope("Sync with {RepoId}@{RepoUrl} Task Id: {TaskId}", repoId, repo.UpStreamUrl,
                   taskId))
        {
            var stopWatch = Stopwatch.StartNew();

            await using var transaction = await defaultDbContext.Database.BeginTransactionAsync();

            try
            {
                await StartSyncInternal(repo.UpStreamUrl, repo.Id, taskLogger);

                await transaction.CommitAsync();
            }
            catch (Exception e)
            {
                taskLogger.LogError(e, "Error while Syncing Repo {RepoId}", repoId);
                await transaction.RollbackAsync();

                await repoSyncTaskService.UpdateSyncTaskAsync(taskId, DateTimeOffset.Now, SyncTaskStatus.Failed);
                return;
            }
            finally
            {
                stopWatch.Stop();
                taskLogger.LogInformation("Sync Task Finish in {Elapsed}", stopWatch.Elapsed);
            }

            await repoSyncTaskService.UpdateSyncTaskAsync(taskId, DateTimeOffset.Now, SyncTaskStatus.Completed);
        }
    }

    private async Task StartSyncInternal(string sourceRepoUrl, string sourceRepoId, ILogger taskLogger)
    {
        taskLogger.LogInformation("Start Sync with: {SourceRepoId}@{RepoUrl}", sourceRepoId, sourceRepoUrl);

        // Fetch Repo MetaData
        var repo = await FetchRepoAsync(sourceRepoUrl);

        // Count Packages
        var packagesCount = repo.Packages
            .SelectMany(package =>
                package.Value.Versions.Select(version => version.Value))
            .Count();
        taskLogger.LogInformation("Found {PackageCount} Packages", packagesCount);

        // Sync Packages
        foreach (var package in repo.Packages.SelectMany(
                     package => package.Value.Versions.Select(version => version.Value)))
        {
            var fileId = await ProcessPackageFileAsync(package, sourceRepoId, taskLogger);

            await repoMetaDataService.MarkAddOrUpdateVpmPackageAsync(package, fileId, sourceRepoId,
                repo.Id ?? sourceRepoId);
            taskLogger.LogInformation("Add {PackageName}@{PackageVersion} to DataBase", package.Name, package.Version);
        }

        await defaultDbContext.SaveChangesAsync();
    }

    private ILogger<RepoSynchronizerService> GetTaskLogger(ILogger parentLogger, string logPath)
    {
        var rawTaskLogger = new LoggerConfiguration()
            .WriteTo.ILogger(parentLogger)
            .WriteTo.File(new ExpressionTemplate(logTemplate), logPath,
                rollingInterval: RollingInterval.Infinite)
            .CreateLogger().ForContext<RepoSynchronizerService>();

        return new SerilogLoggerFactory(rawTaskLogger).CreateLogger<RepoSynchronizerService>();
    }

    private async ValueTask<VpmRepo> FetchRepoAsync(string sourceRepoUrl)
    {
        var repoResponse = await httpClient.GetStringAsync(sourceRepoUrl);
        var repo = JsonSerializer.Deserialize<VpmRepo>(repoResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        if (repo is null)
        {
            throw new InvalidOperationException("Deserialize Repo Response is not valid");
        }

        return repo;
    }

    private async ValueTask<string> ProcessPackageFileAsync(VpmPackage package, string sourceRepoId, ILogger taskLogger)
    {
        var sha256 = package.ZipSha256;
        var fileName = $"{package.Name}@{package.Version}@{sourceRepoId}.zip";

        if (sha256 is null)
        {
            taskLogger.LogWarning(
                "Package {PackageName}@{PackageVersion} have not ZipSha256, we will download it anyway if it's not downloaded before",
                package.Name, package.Version);
        }

        if (await repoMetaDataService.GetVpmPackage(package.Name, package.Version) is not { } packageEntity)
        {
            if (sha256 is null || await fileHostService.LookupFileByHashAsync(sha256) is not { } fileId)
            {
                return await DownloadAndUploadFileAsync(package.Url, fileName, taskLogger, sha256);
            }

            taskLogger.LogInformation(
                "File with same ZipSha256 is already Downloaded & Uploaded, Skip Download {PackageName}@{PackageVersion}",
                package.Name,
                package.Version);

            return fileId;
        }

        if (!await fileHostService.IsFileExist(packageEntity.FileId))
        {
            taskLogger.LogWarning(
                "Package {PackageName}@{PackageVersion} have not ZipSha256, although the package already have a fileId ({OriginFileId}), " +
                "but the file id is not exist in File Host Service, " +
                "so we will download it anyway",
                package.Name, package.Version, packageEntity.FileId);

            taskLogger.LogInformation(
                "Start Downloading {PackageName}@{PackageVersion}@{SourceRepoId}: {PackageUrl}",
                package.Name,
                package.Version, sourceRepoId, package.Url);

            return await DownloadAndUploadFileAsync(package.Url, fileName, taskLogger, sha256);
        }

        if (sha256 is null)
        {
            taskLogger.LogInformation(
                "We found the package file is already Downloaded & Uploaded and the package have not ZipSha256, so we Skip Download {PackageName}@{PackageVersion}",
                package.Name,
                package.Version);

            return packageEntity.FileId;
        }

        if (sha256 == packageEntity.ZipSha256)
        {
            taskLogger.LogInformation(
                "We found the package file is already Downloaded & Uploaded and the package have the same ZipSha256, so we Skip Download {PackageName}@{PackageVersion}",
                package.Name,
                package.Version);

            return packageEntity.FileId;
        }

        if (packageEntity.ZipSha256 != sha256)
        {
            taskLogger.LogWarning(
                "Package {PackageName}@{PackageVersion} have different ZipSha256 compare with exist package MeatData, " +
                "we will overwrite the exist one with remote one, Exist: {ExistSha256}, Remote: {RemoteSha256}",
                package.Name, package.Version, packageEntity.ZipSha256, package.ZipSha256);
        }

        taskLogger.LogInformation(
            "Start Downloading {PackageName}@{PackageVersion}@{SourceRepoId}: {PackageUrl}",
            package.Name,
            package.Version, sourceRepoId, package.Url);

        return await DownloadAndUploadFileAsync(package.Url, fileName,
            taskLogger, sha256);
    }

    private async ValueTask<string> DownloadAndUploadFileAsync(string url, string fileName, ILogger taskLogger,
        string? hash)
    {
        var tempFileName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        await using var stream = await httpClient.GetStreamAsync(url);
        await using var tempFileStream = File.Create(tempFileName);
        await stream.CopyToAsync(tempFileStream);

        tempFileStream.Close();

        taskLogger.LogInformation("Downloaded {FileName} From {Url}", fileName, url);

        var fileHash = await FileUtils.HashFile(tempFileName);

        taskLogger.LogInformation("Downloaded {FileName} Hash: {FileHash}", fileName, fileHash);
        if (hash is not null)
        {
            if (fileHash != hash)
            {
                taskLogger.LogError(
                    "Downloaded File Hash is not match with Provided Hash, the file may be corrupted, Expected: {ExpectedHash}, Actual: {ActualHash}",
                    hash, fileHash);

                File.Delete(tempFileName);
                throw new InvalidOperationException("Downloaded File Hash is not match with Provided Hash");
            }

            taskLogger.LogInformation("Downloaded File Hash Match with Provided Hash, Continue Upload");
        }
        else
        {
            taskLogger.LogInformation("No Hash Provided, Skip Hash Check");
        }

        if (await fileHostService.LookupFileByHashAsync(fileHash) is not { } fileRecordId)
        {
            taskLogger.LogInformation(
                "Uploading {FileName} to File Host Service", fileName);
            var fileId = await fileHostService.UploadFileAsync(tempFileName, fileName);
            taskLogger.LogInformation("Uploaded {FileName} to File Host Service", fileName);

            return fileId;
        }

        taskLogger.LogInformation(
            "File is already Uploaded, Skip Upload {FileName}", fileName);

        File.Delete(tempFileName);

        return fileRecordId;
    }
}