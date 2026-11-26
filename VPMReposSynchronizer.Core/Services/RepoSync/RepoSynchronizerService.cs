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
            try
            {
                var processed = await ProcessPackageFileAsync(package, sourceRepoId, taskLogger);

                await repoMetaDataService.MarkAddOrUpdateVpmPackageAsync(package, processed.FileId, sourceRepoId,
                    processed.ContentSha256);
                taskLogger.LogInformation("Add {PackageName}@{PackageVersion} to DataBase", package.Name, package.Version);
            }
            catch (PackageSyncException e)
            {
                // 只有内容与上游声明的哈希不一致才跳过该版本；数据库或文件存储等其它异常继续向上抛出。
                taskLogger.LogError(
                    "Skip {PackageName}@{PackageVersion} and keep the existing record: {Reason}",
                    package.Name, package.Version, e.Message);
            }
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

        if (repo is null) throw new InvalidOperationException("Deserialize Repo Response is not valid");

        return repo;
    }

    // FileId: 文件存储返回的 id，语义由具体存储决定（本地存储为哈希，S3 为带文件名的 key），不能当作内容哈希使用。
    // ContentSha256: 应记录到镜像的哈希，即上游声明的值，或上游未声明时对下载内容实算的值。
    private record struct ProcessedPackageFile(string FileId, string? ContentSha256);

    private async ValueTask<ProcessedPackageFile> ProcessPackageFileAsync(VpmPackage package, string sourceRepoId,
        ILogger taskLogger)
    {
        // 空字符串与字段缺失视为同一种情况：上游没有声明哈希。
        var sha256 = string.IsNullOrEmpty(package.ZipSha256) ? null : package.ZipSha256;
        var fileName = $"{package.Name}@{package.Version}@{sourceRepoId}.zip";

        if (sha256 is null)
            taskLogger.LogWarning(
                "Package {PackageName}@{PackageVersion} have not ZipSha256, we will download it anyway if it's not downloaded before",
                package.Name, package.Version);

        if (sha256 is not null && await fileHostService.LookupFileByHashAsync(sha256) is {} fileId)
        {
            taskLogger.LogInformation("Package {PackageName}@{PackageVersion} is already Downloaded & Uploaded, Skip Download",
                package.Name, package.Version);

            return new ProcessedPackageFile(fileId, sha256);
        }

        if (await repoMetaDataService.GetVpmPackage(package.Name, package.Version, sourceRepoId) is not { } packageEntity)
        {
            taskLogger.LogInformation("Package {PackageName}@{PackageVersion} is not exist in DataBase, Start Downloading",
                package.Name, package.Version);

            var downloaded = await DownloadAndUploadFileAsync(package.Url, fileName, taskLogger, sha256);
            return new ProcessedPackageFile(downloaded.FileId, sha256 ?? downloaded.FileHash);
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

            var downloaded = await DownloadAndUploadFileAsync(package.Url, fileName, taskLogger, sha256);
            return new ProcessedPackageFile(downloaded.FileId, sha256 ?? downloaded.FileHash);
        }

        if (sha256 is null)
        {
            taskLogger.LogInformation(
                "We found the package file is already Downloaded & Uploaded and the package have not ZipSha256, so we Skip Download {PackageName}@{PackageVersion}",
                package.Name,
                package.Version);

            // 复用记录中已有的校验值，避免上游未声明时把之前实算的哈希覆盖掉；
            // 记录本身也没有哈希（历史数据）时保持为空，不从 fileId 猜测。
            return new ProcessedPackageFile(packageEntity.FileId, packageEntity.ZipSha256);
        }

        if (sha256 == packageEntity.ZipSha256)
        {
            taskLogger.LogInformation(
                "We found the package file is already Downloaded & Uploaded and the package have the same ZipSha256, so we Skip Download {PackageName}@{PackageVersion}",
                package.Name,
                package.Version);

            return new ProcessedPackageFile(packageEntity.FileId, sha256);
        }

        if (packageEntity.ZipSha256 != sha256)
        {
            // 上游为已镜像的同一版本声明了新的哈希：下载新文件并按新哈希校验，通过后才更新记录。
            taskLogger.LogWarning(
                "Package {PackageName}@{PackageVersion} have different ZipSha256 compare with exist package MeatData, " +
                "Exist: {ExistSha256}, Remote: {RemoteSha256}, verifying the upstream file before updating",
                package.Name, package.Version, packageEntity.ZipSha256, sha256);
        }

        taskLogger.LogInformation(
            "Start Downloading {PackageName}@{PackageVersion}@{SourceRepoId}: {PackageUrl}",
            package.Name,
            package.Version, sourceRepoId, package.Url);

        var verified = await DownloadAndUploadFileAsync(package.Url, fileName, taskLogger, sha256);
        return new ProcessedPackageFile(verified.FileId, sha256 ?? verified.FileHash);
    }

    private async ValueTask<(string FileId, string FileHash)> DownloadAndUploadFileAsync(string url, string fileName,
        ILogger taskLogger,
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
                throw new PackageSyncException(
                    $"Downloaded file hash {fileHash} does not match the declared hash {hash} for {fileName}");
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

            return (fileId, fileHash);
        }

        taskLogger.LogInformation(
            "File is already Uploaded, Skip Upload {FileName}", fileName);

        File.Delete(tempFileName);

        return (fileRecordId, fileHash);
    }
}
