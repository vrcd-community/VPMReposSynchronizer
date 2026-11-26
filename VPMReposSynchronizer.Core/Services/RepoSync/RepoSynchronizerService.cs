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
    public static readonly string SyncTaskLoggerPath = Path.Combine("sync-tasks-logs");

    private const string logTemplate =
        "[{@t:yyyy-MM-dd HH:mm:ss} " +
        "{@l:u3}]" +
        "{#if SourceContext is not null} [{Substring(SourceContext, LastIndexOf(SourceContext, '.') + 1)}]{#end}" +
        " {@m}" +
        "\n{@x}";

    public async Task StartSync(long taskId)
    {
        var task = await repoSyncTaskService.GetSyncTaskAsync(taskId);

        if (task is null)
        {
            throw new InvalidOperationException("Task not found");
        }

        var repoId = task.RepoId;

        var repo = await repoMetaDataService.GetRepoById(repoId);
        if (repo is null) throw new InvalidOperationException($"Repo with id {repoId} not found");

        var logPath = Path.Combine(SyncTaskLoggerPath,
            repoId, $"{taskId}-{repoId}-{DateTimeOffset.Now:yyyy-MM-dd-HH-mm-ss}.log");
        var taskLogger = GetTaskLogger(logger, logPath);

        await repoSyncTaskService.UpdateSyncTaskAsync(taskId, logPath);

        using (taskLogger.BeginScope("Sync with {RepoId}@{RepoUrl} Task Id: {TaskId}", repoId, repo.UpStreamUrl,
                   taskId))
        {
            var stopWatch = Stopwatch.StartNew();

            try
            {
                await StartSyncInternal(repo.UpStreamUrl, repo.Id, taskLogger);
            }
            catch (Exception e)
            {
                taskLogger.LogError(e, "Error while Syncing Repo {RepoId}", repoId);

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

        // Update Repo MetaData
        await repoMetaDataService.UpdateRepoAsync(sourceRepoId, repo);

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

            await repoMetaDataService.MarkAddOrUpdateVpmPackageAsync(package, fileId, sourceRepoId);
            taskLogger.LogInformation("Add {PackageName}@{PackageVersion} to DataBase", package.Name, package.Version);
        }

        await defaultDbContext.SaveChangesAsync();

        // 清单在 FetchRepoAsync 已校验为显式结构有效的对象，因此空对象即“上游撤回了全部版本”。
        // 只清本源索引：文件按内容哈希寻址不删除，其它来源的记录不受影响。
        // 用条目内声明的 name/version：镜像记录正是按它们落库，校验已保证其与清单键一致。
        var keepPackages = repo.Packages
            .SelectMany(package => package.Value.Versions
                .Select(version => (version.Value.Name, version.Value.Version)))
            .ToHashSet();

        var removedCount = await repoMetaDataService.RemoveVpmPackagesNotInAsync(sourceRepoId, keepPackages);
        if (removedCount > 0)
        {
            taskLogger.LogInformation("Removed {RemovedCount} retracted package versions from {SourceRepoId}",
                removedCount, sourceRepoId);

            await defaultDbContext.SaveChangesAsync();
        }
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

        using var document = JsonDocument.Parse(repoResponse);
        ValidateManifestStructure(document);

        var repo = JsonSerializer.Deserialize<VpmRepo>(repoResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        if (repo is null) throw new InvalidOperationException("Deserialize Repo Response is not valid");

        return repo;
    }

    /// <summary>
    /// 在写入任何数据之前校验清单结构。只有显式且结构有效的 packages 对象才算有效清单：
    /// 缺字段、null 或类型错误都不是“上游撤回了所有版本”，必须让本次同步失败，
    /// 而不是把镜像当空清单清理。属性名按与 JsonSerializer（CamelCase 命名策略）一致的方式进行精确匹配。
    /// 版本条目内声明的 name/version 必须与其清单键一致：镜像记录按条目内的值落库，
    /// 不一致会写出与清单对不上的记录。
    /// </summary>
    private static void ValidateManifestStructure(JsonDocument document)
    {
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Repo Response is not a JSON object");

        if (!TryGetProperty(document.RootElement, "packages", out var packagesElement) ||
            packagesElement.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Repo Response does not contain a valid \"packages\" object");

        foreach (var packageEntry in packagesElement.EnumerateObject())
        {
            if (packageEntry.Value.ValueKind != JsonValueKind.Object ||
                !TryGetProperty(packageEntry.Value, "versions", out var versionsElement) ||
                versionsElement.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException(
                    $"Package entry \"{packageEntry.Name}\" does not contain a valid \"versions\" object");

            foreach (var versionEntry in versionsElement.EnumerateObject())
            {
                if (versionEntry.Value.ValueKind != JsonValueKind.Object ||
                    !TryGetString(versionEntry.Value, "name", out var name) ||
                    !TryGetString(versionEntry.Value, "version", out var version) ||
                    name != packageEntry.Name ||
                    version != versionEntry.Name)
                    throw new InvalidOperationException(
                        $"Package entry \"{packageEntry.Name}/{versionEntry.Name}\" does not match the name/version it declares");
            }
        }
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.Ordinal))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static bool TryGetString(JsonElement element, string name, out string? value)
    {
        value = null;

        if (!TryGetProperty(element, name, out var property) || property.ValueKind != JsonValueKind.String)
            return false;

        value = property.GetString();
        return true;
    }

    private async ValueTask<string> ProcessPackageFileAsync(VpmPackage package, string sourceRepoId, ILogger taskLogger)
    {
        var sha256 = package.ZipSha256;
        var fileName = $"{package.Name}@{package.Version}@{sourceRepoId}.zip";

        if (sha256 is null)
            taskLogger.LogWarning(
                "Package {PackageName}@{PackageVersion} have not ZipSha256, we will download it anyway if it's not downloaded before",
                package.Name, package.Version);

        if (sha256 is not null && await fileHostService.LookupFileByHashAsync(sha256) is {} fileId)
        {
            taskLogger.LogInformation("Package {PackageName}@{PackageVersion} is already Downloaded & Uploaded, Skip Download",
                package.Name, package.Version);

            return fileId;
        }

        if (await repoMetaDataService.GetVpmPackage(package.Name, package.Version, sourceRepoId) is not { } packageEntity)
        {
            taskLogger.LogInformation("Package {PackageName}@{PackageVersion} is not exist in DataBase, Start Downloading",
                package.Name, package.Version);

            return await DownloadAndUploadFileAsync(package.Url, fileName, taskLogger, sha256);
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
            taskLogger.LogError(
                "Package {PackageName}@{PackageVersion} have different ZipSha256 compare with exist package MeatData, " +
                "Exist: {ExistSha256}, Remote: {RemoteSha256}",
                package.Name, package.Version, packageEntity.ZipSha256, package.ZipSha256);

            throw new InvalidOperationException("Package ZipSha256 is not match with exist package MeatData");
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
