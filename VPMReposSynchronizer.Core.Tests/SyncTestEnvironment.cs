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

namespace VPMReposSynchronizer.Core.Tests;

/// <summary>
/// Minimal in-process harness: real SQLite file database, real repository/sync services,
/// fake file host and fake HTTP transport.
/// </summary>
internal sealed class SyncTestEnvironment : IDisposable
{
    public const string RepoId = "test-repo";
    public const string IndexUrl = "https://upstream.test/index.json";

    private readonly string _directory;
    private readonly MapperConfiguration _mapperConfiguration;

    public SyncTestEnvironment()
    {
        _directory = Path.Combine(Path.GetTempPath(), "vpmsync-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        DatabasePath = Path.Combine(_directory, "sync.db");

        Db = CreateContext();
        Db.Database.EnsureCreated();

        _mapperConfiguration = new MapperConfiguration(config => config.AddProfile<VpmPackageProfile>());
        Mapper = _mapperConfiguration.CreateMapper();

        var repoMetaDataService = new RepoMetaDataService(Db, null!, Mapper);
        TaskService = new RepoSyncTaskService(Db);
        Synchronizer = new RepoSynchronizerService(
            repoMetaDataService,
            TaskService,
            FileHost,
            NullLogger<RepoSynchronizerService>.Instance,
            new HttpClient(Handler),
            Db);
    }

    public string DatabasePath { get; }
    public IMapper Mapper { get; }
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

    public async Task<long> SeedRepoAsync()
    {
        // Seed through a separate context so the synchronizer's own context does not track
        // entities that production would only ever read with AsNoTracking.
        await using (var context = CreateContext())
        {
            context.Repos.Add(new VpmRepoEntity
            {
                Id = RepoId,
                Description = "test repo",
                UpStreamUrl = IndexUrl,
                SyncTaskCron = "0 0 * * *"
            });
            await context.SaveChangesAsync();
        }

        return await TaskService.AddSyncTaskAsync(RepoId, "unused-in-tests");
    }

    public async Task SeedMirroredPackageAsync(string name, string version, string? sha256, string fileId)
    {
        await using (var context = CreateContext())
        {
            context.Packages.Add(new VpmPackageEntity
            {
                PackageId = $"{name}@{version}@{RepoId}",
                UpstreamId = RepoId,
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

    public VpmRepo BuildRepo(params VpmPackage[] packages)
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

    public async Task<VpmPackageEntity?> ReadPackageAsync(string name, string version)
    {
        await using var context = CreateContext();
        var packageId = $"{name}@{version}@{RepoId}";
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

    public int UploadCount { get; private set; }

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
        UploadCount++;
        var hash = SyncTestEnvironment.Hash(File.ReadAllBytes(path));
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

    public List<string> RequestedUrls { get; } = [];

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
        RequestedUrls.Add(url);

        if (!_routes.TryGetValue(url, out var route))
        {
            throw new HttpRequestException($"No route configured for {url}");
        }

        return Task.FromResult(route());
    }
}
