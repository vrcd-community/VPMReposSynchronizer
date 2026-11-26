using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VPMReposSynchronizer.Core.DbContexts;
using VPMReposSynchronizer.Core.Models.Entity;
using VPMReposSynchronizer.Core.Models.Mappers;
using VPMReposSynchronizer.Core.Models.Types;
using VPMReposSynchronizer.Core.Services;
using VPMReposSynchronizer.Core.Services.FileHost;
using VPMReposSynchronizer.Core.Services.RepoSync;

namespace VPMReposSynchronizer.Core.RetractionTests;

/// <summary>
/// In-process harness with a real SQLite file database, the real repository/sync services,
/// a fake content-addressed file host and a fake HTTP transport.
/// </summary>
internal sealed class RetractionSyncTestEnvironment : IDisposable
{
    public const string RepoId = "test-repo";
    public const string IndexUrl = "https://upstream.test/index.json";

    private readonly string _directory;

    public RetractionSyncTestEnvironment()
    {
        _directory = Path.Combine(Path.GetTempPath(), "vpmretract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        DatabasePath = Path.Combine(_directory, "sync.db");

        Db = CreateContext();
        Db.Database.EnsureCreated();

        var mapper = new MapperConfiguration(config => config.AddProfile<VpmPackageProfile>()).CreateMapper();
        var repoMetaDataService = new RepoMetaDataService(Db, null!, mapper);

        var taskService = new RepoSyncTaskService(Db);
        TaskService = taskService;
        Synchronizer = new RepoSynchronizerService(
            repoMetaDataService,
            taskService,
            FileHost,
            NullLogger<RepoSynchronizerService>.Instance,
            new HttpClient(Handler),
            Db);
    }

    public string DatabasePath { get; }
    public FakeFileHostService FileHost { get; } = new();
    public FakeHttpMessageHandler Handler { get; } = new();
    public DefaultDbContext Db { get; }
    public RepoSyncTaskService TaskService { get; }
    public RepoSynchronizerService Synchronizer { get; }

    public DefaultDbContext CreateContext()
    {
        return new DefaultDbContext(
            new DbContextOptionsBuilder<DefaultDbContext>()
                .UseSqlite($"Data Source={DatabasePath}")
                .Options);
    }

    public async Task SeedRepoAsync()
    {
        await using var context = CreateContext();
        context.Repos.Add(new VpmRepoEntity
        {
            Id = RepoId,
            Description = "test repo",
            UpStreamUrl = IndexUrl,
            SyncTaskCron = "0 0 * * *"
        });
        await context.SaveChangesAsync();
    }

    public async Task SeedMirroredPackageAsync(string name, string version, string sha256, string fileId,
        string repoId = RepoId)
    {
        await using (var context = CreateContext())
        {
            context.Packages.Add(new VpmPackageEntity
            {
                PackageId = $"{name}@{version}@{repoId}",
                UpstreamId = repoId,
                Name = name,
                Version = version,
                FileId = fileId,
                ZipSha256 = sha256
            });
            await context.SaveChangesAsync();
        }

        FileHost.RegisterExistingFile(fileId, sha256);
    }

    public async Task<long> RunSyncAsync()
    {
        var taskId = await TaskService.AddSyncTaskAsync(RepoId, "unused-in-tests");
        await Synchronizer.StartSync(taskId);
        return taskId;
    }

    public static VpmPackage Package(string name, string version, string url, string? sha256)
    {
        return new VpmPackage
        {
            Name = name,
            Version = version,
            Url = url,
            ZipSha256 = sha256
        };
    }

    public static VpmRepo BuildRepo(params VpmPackage[] packages)
    {
        var grouped = packages
            .GroupBy(package => package.Name)
            .ToDictionary(
                group => group.Key,
                group => new VpmRepoPackageVersions(
                    group.ToDictionary(package => package.Version, package => package)));

        return new VpmRepo("Test Repo", "Test Author", IndexUrl, RepoId, grouped);
    }

    public void ServeRepoIndex(VpmRepo repo)
    {
        Handler.MapJson(IndexUrl, JsonSerializer.Serialize(repo, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }

    public void ServeRawIndex(string json)
    {
        Handler.MapJson(IndexUrl, json);
    }

    public static byte[] Content(string text)
    {
        return Encoding.UTF8.GetBytes(text);
    }

    public static string Hash(byte[] content)
    {
        return Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
    }

    public async Task<SyncTaskEntity?> ReadTaskAsync(long taskId)
    {
        await using var context = CreateContext();
        return await context.SyncTasks.AsNoTracking().SingleOrDefaultAsync(task => task.Id == taskId);
    }

    public async Task<VpmPackageEntity?> ReadPackageAsync(string name, string version, string repoId = RepoId)
    {
        await using var context = CreateContext();
        var packageId = $"{name}@{version}@{repoId}";
        return await context.Packages.AsNoTracking().SingleOrDefaultAsync(package => package.PackageId == packageId);
    }

    public void Dispose()
    {
        Db.Dispose();
        try
        {
            Directory.Delete(_directory, true);
        }
        catch (IOException)
        {
            // best effort cleanup of the temporary database directory
        }
    }
}

internal sealed class FakeFileHostService : IFileHostService
{
    private readonly Dictionary<string, string> _hashToFileId = new();
    private readonly HashSet<string> _fileIds = new();

    public void RegisterExistingFile(string fileId, string? sha256)
    {
        _fileIds.Add(fileId);
        if (sha256 is not null)
        {
            _hashToFileId[sha256] = fileId;
        }
    }

    public Task<string> UploadFileAsync(string path, string name)
    {
        var hash = RetractionSyncTestEnvironment.Hash(File.ReadAllBytes(path));
        if (_hashToFileId.TryGetValue(hash, out var existing))
        {
            return Task.FromResult(existing);
        }

        var fileId = "file-" + Guid.NewGuid().ToString("N");
        _hashToFileId[hash] = fileId;
        _fileIds.Add(fileId);
        return Task.FromResult(fileId);
    }

    public Task<string> GetFileUriAsync(string fileId)
    {
        return Task.FromResult("https://files.test/" + fileId);
    }

    public Task<string?> LookupFileByHashAsync(string hash)
    {
        return Task.FromResult(_hashToFileId.TryGetValue(hash, out var fileId) ? fileId : null);
    }

    public Task<bool> IsFileExist(string fileId)
    {
        return Task.FromResult(_fileIds.Contains(fileId));
    }
}

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpResponseMessage>> _routes = new();

    public void MapContent(string url, byte[] content)
    {
        _routes[url] = () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(content)
        };
    }

    public void MapJson(string url, string json)
    {
        _routes[url] = () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    public void MapStatus(string url, HttpStatusCode statusCode)
    {
        _routes[url] = () => new HttpResponseMessage(statusCode);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.ToString();

        if (!_routes.TryGetValue(url, out var route))
        {
            throw new HttpRequestException($"No route configured for {url}");
        }

        return Task.FromResult(route());
    }
}
