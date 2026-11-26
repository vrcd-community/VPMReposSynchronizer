using System.Net;
using VPMReposSynchronizer.Core.Models.Entity;
using Xunit;

namespace VPMReposSynchronizer.Core.RetractionTests;

/// <summary>
/// Retracted package cleanup: when the upstream manifest is fetched completely and its
/// <c>packages</c> object is structurally valid, package versions that are no longer listed
/// are removed from this source's mirror metadata. Files (content addressed) and records of
/// other sources are left alone. Invalid or unavailable manifests must never delete anything.
/// </summary>
public class RetractedPackageCleanupTests
{
    private const string Name = "com.example.a";
    private const string UrlSecond = "https://upstream.test/a-2.0.0.zip";

    [Fact]
    public async Task RetractedVersion_IsRemoved_FileAndRemainingVersionsAreKept()
    {
        using var env = new RetractionSyncTestEnvironment();
        await env.SeedRepoAsync();

        var retractedHash = RetractionSyncTestEnvironment.Hash(
            RetractionSyncTestEnvironment.Content("version-1"));
        var keptHash = RetractionSyncTestEnvironment.Hash(
            RetractionSyncTestEnvironment.Content("version-2"));

        await env.SeedMirroredPackageAsync(Name, "1.0.0", retractedHash, "file-old");
        await env.SeedMirroredPackageAsync(Name, "2.0.0", keptHash, "file-kept");
        env.ServeRepoIndex(RetractionSyncTestEnvironment.BuildRepo(
            RetractionSyncTestEnvironment.Package(Name, "2.0.0", UrlSecond, keptHash)));

        var taskId = await env.RunSyncAsync();

        Assert.Equal(SyncTaskStatus.Completed, (await env.ReadTaskAsync(taskId))!.Status);
        Assert.Null(await env.ReadPackageAsync(Name, "1.0.0"));

        var kept = await env.ReadPackageAsync(Name, "2.0.0");
        Assert.NotNull(kept);
        Assert.Equal(keptHash, kept!.ZipSha256);
        Assert.Equal("file-kept", kept.FileId);

        // The archive of the retracted version is content addressed and is not deleted.
        Assert.Equal("file-old", await env.FileHost.LookupFileByHashAsync(retractedHash));
    }

    [Fact]
    public async Task ExplicitEmptyPackageList_ClearsOnlyThisSource()
    {
        using var env = new RetractionSyncTestEnvironment();
        await env.SeedRepoAsync();

        var mirroredHash = RetractionSyncTestEnvironment.Hash(
            RetractionSyncTestEnvironment.Content("version-1"));
        var otherSourceHash = RetractionSyncTestEnvironment.Hash(
            RetractionSyncTestEnvironment.Content("version-1-other"));

        await env.SeedMirroredPackageAsync(Name, "1.0.0", mirroredHash, "file-old");
        await env.SeedMirroredPackageAsync(Name, "1.0.0", otherSourceHash, "file-other", "other-repo");

        // An explicitly empty packages object means the source retracted everything.
        env.ServeRepoIndex(RetractionSyncTestEnvironment.BuildRepo());

        var taskId = await env.RunSyncAsync();

        Assert.Equal(SyncTaskStatus.Completed, (await env.ReadTaskAsync(taskId))!.Status);
        Assert.Null(await env.ReadPackageAsync(Name, "1.0.0"));

        var otherSource = await env.ReadPackageAsync(Name, "1.0.0", "other-repo");
        Assert.NotNull(otherSource);
        Assert.Equal(otherSourceHash, otherSource!.ZipSha256);

        Assert.Equal("file-old", await env.FileHost.LookupFileByHashAsync(mirroredHash));
        Assert.Equal("file-other", await env.FileHost.LookupFileByHashAsync(otherSourceHash));
    }

    [Theory]
    [InlineData("{\"name\":\"Test Repo\",\"author\":\"Test Author\",\"url\":\"https://upstream.test/index.json\"}")]
    [InlineData("{\"name\":\"Test Repo\",\"author\":\"Test Author\",\"url\":\"https://upstream.test/index.json\",\"packages\":null}")]
    [InlineData("{\"name\":\"Test Repo\",\"author\":\"Test Author\",\"url\":\"https://upstream.test/index.json\",\"packages\":[]}")]
    [InlineData("{\"name\":\"Test Repo\",\"author\":\"Test Author\",\"url\":\"https://upstream.test/index.json\",\"packages\":\"none\"}")]
    [InlineData("{\"name\":\"Test Repo\",\"author\":\"Test Author\",\"url\":\"https://upstream.test/index.json\",\"Packages\":{}}")]
    public async Task InvalidPackageList_FailsWithoutRemovingAnything(string manifest)
    {
        using var env = new RetractionSyncTestEnvironment();
        await env.SeedRepoAsync();

        var mirroredHash = RetractionSyncTestEnvironment.Hash(
            RetractionSyncTestEnvironment.Content("version-1"));
        await env.SeedMirroredPackageAsync(Name, "1.0.0", mirroredHash, "file-old");

        env.ServeRawIndex(manifest);

        var taskId = await env.RunSyncAsync();

        Assert.Equal(SyncTaskStatus.Failed, (await env.ReadTaskAsync(taskId))!.Status);

        var mirrored = await env.ReadPackageAsync(Name, "1.0.0");
        Assert.NotNull(mirrored);
        Assert.Equal(mirroredHash, mirrored!.ZipSha256);
        Assert.Equal("file-old", mirrored.FileId);
    }

    [Theory]
    [InlineData("{\"packages\":{\"com.example.a\":{}}}")]
    [InlineData("{\"packages\":{\"com.example.a\":{\"versions\":[]}}}")]
    [InlineData("{\"packages\":{\"com.example.a\":{\"versions\":\"none\"}}}")]
    public async Task InvalidVersionsEntry_FailsWithoutRemovingAnything(string packagesJson)
    {
        using var env = new RetractionSyncTestEnvironment();
        await env.SeedRepoAsync();

        var mirroredHash = RetractionSyncTestEnvironment.Hash(
            RetractionSyncTestEnvironment.Content("version-1"));
        await env.SeedMirroredPackageAsync(Name, "1.0.0", mirroredHash, "file-old");

        env.ServeRawIndex(
            "{\"name\":\"Test Repo\",\"author\":\"Test Author\",\"url\":\"https://upstream.test/index.json\"," +
            packagesJson + "}");

        var taskId = await env.RunSyncAsync();

        Assert.Equal(SyncTaskStatus.Failed, (await env.ReadTaskAsync(taskId))!.Status);

        var mirrored = await env.ReadPackageAsync(Name, "1.0.0");
        Assert.NotNull(mirrored);
        Assert.Equal(mirroredHash, mirrored!.ZipSha256);
        Assert.Equal("file-old", mirrored.FileId);
    }

    [Fact]
    public async Task ManifestKeyNameMismatch_FailsWithoutWritingAnything()
    {
        using var env = new RetractionSyncTestEnvironment();
        await env.SeedRepoAsync();

        var mirroredContent = RetractionSyncTestEnvironment.Content("version-1");
        var mirroredHash = RetractionSyncTestEnvironment.Hash(mirroredContent);
        await env.SeedMirroredPackageAsync(Name, "1.0.0", mirroredHash, "file-old");
        env.Handler.MapContent("https://upstream.test/a-1.0.0.zip", mirroredContent);

        // The entry is keyed as com.example.a but declares a different name: the mirror would be
        // written under the declared name while the manifest key says otherwise.
        env.ServeRawIndex($$"""
            {
              "name": "Test Repo",
              "author": "Test Author",
              "url": "https://upstream.test/index.json",
              "packages": {
                "{{Name}}": {
                  "versions": {
                    "1.0.0": {
                      "name": "com.other.name",
                      "version": "1.0.0",
                      "url": "https://upstream.test/a-1.0.0.zip",
                      "zipSHA256": "{{mirroredHash}}"
                    }
                  }
                }
              }
            }
            """);

        var taskId = await env.RunSyncAsync();

        Assert.Equal(SyncTaskStatus.Failed, (await env.ReadTaskAsync(taskId))!.Status);
        Assert.Null(await env.ReadPackageAsync("com.other.name", "1.0.0"));

        var mirrored = await env.ReadPackageAsync(Name, "1.0.0");
        Assert.NotNull(mirrored);
        Assert.Equal(mirroredHash, mirrored!.ZipSha256);
        Assert.Equal("file-old", mirrored.FileId);
    }

    [Fact]
    public async Task ManifestKeyVersionMismatch_FailsWithoutWritingAnything()
    {
        using var env = new RetractionSyncTestEnvironment();
        await env.SeedRepoAsync();

        var mirroredContent = RetractionSyncTestEnvironment.Content("version-1");
        var mirroredHash = RetractionSyncTestEnvironment.Hash(mirroredContent);
        await env.SeedMirroredPackageAsync(Name, "1.0.0", mirroredHash, "file-old");
        env.Handler.MapContent("https://upstream.test/a-1.0.0.zip", mirroredContent);

        // The version key is 1.0.0 but the entry declares 2.0.0.
        env.ServeRawIndex($$"""
            {
              "name": "Test Repo",
              "author": "Test Author",
              "url": "https://upstream.test/index.json",
              "packages": {
                "{{Name}}": {
                  "versions": {
                    "1.0.0": {
                      "name": "{{Name}}",
                      "version": "2.0.0",
                      "url": "https://upstream.test/a-1.0.0.zip",
                      "zipSHA256": "{{mirroredHash}}"
                    }
                  }
                }
              }
            }
            """);

        var taskId = await env.RunSyncAsync();

        Assert.Equal(SyncTaskStatus.Failed, (await env.ReadTaskAsync(taskId))!.Status);
        Assert.Null(await env.ReadPackageAsync(Name, "2.0.0"));

        var mirrored = await env.ReadPackageAsync(Name, "1.0.0");
        Assert.NotNull(mirrored);
        Assert.Equal(mirroredHash, mirrored!.ZipSha256);
        Assert.Equal("file-old", mirrored.FileId);
    }

    [Fact]
    public async Task ManifestFetchFailure_RemovesNothing()
    {
        using var env = new RetractionSyncTestEnvironment();
        await env.SeedRepoAsync();

        var mirroredHash = RetractionSyncTestEnvironment.Hash(
            RetractionSyncTestEnvironment.Content("version-1"));
        await env.SeedMirroredPackageAsync(Name, "1.0.0", mirroredHash, "file-old");

        env.Handler.MapStatus(RetractionSyncTestEnvironment.IndexUrl, HttpStatusCode.InternalServerError);

        var taskId = await env.RunSyncAsync();

        Assert.Equal(SyncTaskStatus.Failed, (await env.ReadTaskAsync(taskId))!.Status);
        Assert.NotNull(await env.ReadPackageAsync(Name, "1.0.0"));
    }

    [Fact]
    public async Task CompleteManifest_KeepsEveryMirroredVersion()
    {
        using var env = new RetractionSyncTestEnvironment();
        await env.SeedRepoAsync();

        var hash = RetractionSyncTestEnvironment.Hash(
            RetractionSyncTestEnvironment.Content("version-1"));
        await env.SeedMirroredPackageAsync(Name, "1.0.0", hash, "file-old");

        env.ServeRepoIndex(RetractionSyncTestEnvironment.BuildRepo(
            RetractionSyncTestEnvironment.Package(Name, "1.0.0", UrlSecond, hash)));

        var taskId = await env.RunSyncAsync();

        Assert.Equal(SyncTaskStatus.Completed, (await env.ReadTaskAsync(taskId))!.Status);

        var mirrored = await env.ReadPackageAsync(Name, "1.0.0");
        Assert.NotNull(mirrored);
        Assert.Equal(hash, mirrored!.ZipSha256);
        Assert.Equal("file-old", mirrored.FileId);
    }
}
