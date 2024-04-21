using System.Text.Json;
using FreeScheduler;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using Serilog.Sinks.ILogger;
using Serilog.Templates;
using VPMReposSynchronizer.Core.Models.Entity;
using VPMReposSynchronizer.Core.Models.Types;
using VPMReposSynchronizer.Core.Services.FileHost;
using VPMReposSynchronizer.Core.Utils;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace VPMReposSynchronizer.Core.Services;

public class RepoSynchronizerService(
    RepoMetaDataService repoMetaDataService,
    RepoSyncTaskService repoSyncTaskService,
    IFileHostService fileHostService,
    ILogger<RepoSynchronizerService> logger,
    IHttpClientFactory httpClientFactory)
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
        var rawTaskLogger = new LoggerConfiguration()
            .WriteTo.ILogger(logger)
            .WriteTo.File(new ExpressionTemplate(logTemplate), logPath,
                rollingInterval: RollingInterval.Infinite)
            .CreateLogger().ForContext<RepoSynchronizerService>();

        var taskLogger = new SerilogLoggerFactory(rawTaskLogger).CreateLogger<RepoSynchronizerService>();

        await repoSyncTaskService.UpdateSyncTaskAsync(taskId, logPath);

        using (taskLogger.BeginScope("Sync with {RepoId}@{RepoUrl} Task Id: {TaskId}", repoId, repo.UpStreamUrl,
                   taskId))
        {
            try
            {
                await StartSync(repo.UpStreamUrl, repo.Id, taskLogger);
            }
            catch (Exception e)
            {
                taskLogger.LogError(e, "Error while Syncing Repo {RepoId}", repoId);
                await repoSyncTaskService.UpdateSyncTaskAsync(taskId, DateTimeOffset.Now, SyncTaskStatus.Failed);
                return;
            }

            await repoSyncTaskService.UpdateSyncTaskAsync(taskId, DateTimeOffset.Now, SyncTaskStatus.Completed);
        }
    }

    public async Task StartSync(string sourceRepoUrl, string sourceRepoId, ILogger taskLogger)
    {
        taskLogger.LogInformation("Start Sync with: {SourceRepoId}@{RepoUrl}", sourceRepoId, sourceRepoUrl);

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

        taskLogger.LogInformation("Found {PackageCount} Packages",
            repo.Packages.SelectMany(package => package.Value.Versions.Select(version => version.Value)).Count());

        foreach (var package in repo.Packages.SelectMany(
                     package => package.Value.Versions.Select(version => version.Value)))
        {
            if (package.ZipSha256 is null &&
                await repoMetaDataService.GetVpmPackage(package.Name, package.Version) is { } packageEntity)
            {
                taskLogger.LogWarning(
                    "Package {PackageName}@{PackageVersion}@{SourceRepoId} have not ZipSha256 and the package is already downloaded & uploaded before, so we skip download this package",
                    package.Name,
                    package.Version,
                    sourceRepoId);

                await repoMetaDataService.AddOrUpdateVpmPackageAsync(package, packageEntity.FileId, sourceRepoId,
                    repo.Id ?? sourceRepoId);
                taskLogger.LogInformation("Add {PackageName}@{PackageVersion} to DataBase", package.Name,
                    package.Version);

                continue;
            }

            var fileId = "";
            if (package.ZipSha256 is { } sha256 && await fileHostService.LookupFileByHashAsync(sha256) is
                    { } tempFileId)
            {
                taskLogger.LogInformation(
                    "File is already Downloaded & Uploaded, Skip Download {PackageName}@{PackageVersion}@{SourceRepoId}",
                    package.Name,
                    package.Version,
                    sourceRepoId);
                fileId = tempFileId;
            }

            if (fileId == "")
            {
                taskLogger.LogInformation(
                    "Start Downloading {PackageName}@{PackageVersion}@{SourceRepoId}: {PackageUrl}",
                    package.Name,
                    package.Version, sourceRepoId, package.Url);

                var tempFileName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                await using var stream = await httpClient.GetStreamAsync(package.Url);
                await using var tempFileStream = File.Create(tempFileName);
                await stream.CopyToAsync(tempFileStream);

                tempFileStream.Close();

                taskLogger.LogInformation("Downloaded {PackageName}@{PackageVersion}@{SourceRepoId}", package.Name,
                    package.Version, sourceRepoId);

                var fileHash = await FileUtils.HashFile(tempFileName);
                if (await fileHostService.LookupFileByHashAsync(fileHash) is not { } fileRecordId)
                {
                    taskLogger.LogInformation(
                        "Uploading {PackageName}@{PackageVersion}@{SourceRepoId} to File Host Service", package.Name,
                        package.Version, sourceRepoId);
                    var fileName = Path.GetFileName(new Uri(package.Url).AbsolutePath);
                    fileId = await fileHostService.UploadFileAsync(tempFileName, fileName);
                    taskLogger.LogInformation(
                        "Uploaded {PackageName}@{PackageVersion}@{SourceRepoId} to File Host Service",
                        package.Name,
                        package.Version, sourceRepoId);
                }
                else
                {
                    taskLogger.LogInformation(
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
            taskLogger.LogInformation("Add {PackageName}@{PackageVersion} to DataBase", package.Name, package.Version);
        }
    }
}