using System.Net;
using VPMReposSynchronizer.Core.Models.Entity;
using VPMReposSynchronizer.Core.Models.Types;
using Xunit;

namespace VPMReposSynchronizer.Core.Tests;

/// <summary>
/// Focused scenarios for the sync-time SHA-256 handling:
/// a mirrored version whose upstream hash changed is only updated after the newly
/// downloaded archive matches the new hash; a declared/downloaded conflict keeps the
/// old record and does not block the other versions in the same run.
/// </summary>
public class RepoSynchronizerServiceHashConflictTests
{
    private const string Name = "com.example.a";
    private const string UrlOld = "https://upstream.test/a-1.0.0-old.zip";
    private const string UrlNew = "https://upstream.test/a-1.0.0-new.zip";
    private const string UrlSecond = "https://upstream.test/a-2.0.0.zip";

    [Fact]
    public async Task ChangedHash_ContentMatches_AdoptsNewFile()
    {
        using var env = new SyncTestEnvironment();
        var taskId = await env.SeedRepoAsync();

        var oldContent = SyncTestEnvironment.Content("version-1-old");
        var newContent = SyncTestEnvironment.Content("version-1-new");
        var oldHash = SyncTestEnvironment.Hash(oldContent);
        var newHash = SyncTestEnvironment.Hash(newContent);

        await env.SeedMirroredPackageAsync(Name, "1.0.0", oldHash, "file-old");
        env.Handler.MapContent(UrlNew, newContent);
        env.ServeRepoIndex(env.BuildRepo(
            SyncTestEnvironment.Package(Name, "1.0.0", UrlNew, newHash)));

        await env.Synchronizer.StartSync(taskId);

        var task = await env.ReadTaskAsync(taskId);
        Assert.Equal(SyncTaskStatus.Completed, task!.Status);

        var package = await env.ReadPackageAsync(Name, "1.0.0");
        Assert.NotNull(package);
        Assert.Equal(newHash, package!.ZipSha256);
        Assert.NotEqual("file-old", package.FileId);
        Assert.Equal(package.FileId, await env.FileHost.LookupFileByHashAsync(newHash));
    }

    [Fact]
    public async Task ChangedHash_ContentDoesNotMatchNewHash_KeepsOldRecordAndSyncsOthers()
    {
        using var env = new SyncTestEnvironment();
        var taskId = await env.SeedRepoAsync();

        var oldContent = SyncTestEnvironment.Content("version-1-old");
        var downloadedContent = SyncTestEnvironment.Content("version-1-new");
        var secondContent = SyncTestEnvironment.Content("version-2");
        var oldHash = SyncTestEnvironment.Hash(oldContent);
        var declaredHash = SyncTestEnvironment.Hash(SyncTestEnvironment.Content("never-served"));
        var secondHash = SyncTestEnvironment.Hash(secondContent);

        await env.SeedMirroredPackageAsync(Name, "1.0.0", oldHash, "file-old");
        env.Handler.MapContent(UrlNew, downloadedContent);
        env.Handler.MapContent(UrlSecond, secondContent);
        env.ServeRepoIndex(env.BuildRepo(
            SyncTestEnvironment.Package(Name, "1.0.0", UrlNew, declaredHash),
            SyncTestEnvironment.Package(Name, "2.0.0", UrlSecond, secondHash)));

        await env.Synchronizer.StartSync(taskId);

        var rejected = await env.ReadPackageAsync(Name, "1.0.0");
        Assert.NotNull(rejected);
        Assert.Equal(oldHash, rejected!.ZipSha256);
        Assert.Equal("file-old", rejected.FileId);

        var synced = await env.ReadPackageAsync(Name, "2.0.0");
        Assert.NotNull(synced);
        Assert.Equal(secondHash, synced!.ZipSha256);
    }

    [Fact]
    public async Task NewVersion_BadContent_IsRejectedAndOtherVersionsArePersisted()
    {
        using var env = new SyncTestEnvironment();
        var taskId = await env.SeedRepoAsync();

        var badContent = SyncTestEnvironment.Content("corrupted");
        var goodContent = SyncTestEnvironment.Content("version-2");
        var declaredHash = SyncTestEnvironment.Hash(SyncTestEnvironment.Content("expected"));
        var goodHash = SyncTestEnvironment.Hash(goodContent);

        env.Handler.MapContent(UrlNew, badContent);
        env.Handler.MapContent(UrlSecond, goodContent);
        env.ServeRepoIndex(env.BuildRepo(
            SyncTestEnvironment.Package(Name, "1.0.0", UrlNew, declaredHash),
            SyncTestEnvironment.Package(Name, "2.0.0", UrlSecond, goodHash)));

        await env.Synchronizer.StartSync(taskId);

        Assert.Null(await env.ReadPackageAsync(Name, "1.0.0"));

        var synced = await env.ReadPackageAsync(Name, "2.0.0");
        Assert.NotNull(synced);
        Assert.Equal(goodHash, synced!.ZipSha256);
        Assert.Equal(await env.FileHost.LookupFileByHashAsync(goodHash), synced.FileId);
    }

    [Fact]
    public async Task SameHash_SkipsDownload()
    {
        using var env = new SyncTestEnvironment();
        var taskId = await env.SeedRepoAsync();

        var content = SyncTestEnvironment.Content("version-1");
        var hash = SyncTestEnvironment.Hash(content);

        await env.SeedMirroredPackageAsync(Name, "1.0.0", hash, "file-old");
        env.ServeRepoIndex(env.BuildRepo(
            SyncTestEnvironment.Package(Name, "1.0.0", UrlNew, hash)));

        await env.Synchronizer.StartSync(taskId);

        Assert.DoesNotContain(UrlNew, env.Handler.RequestedUrls);

        var package = await env.ReadPackageAsync(Name, "1.0.0");
        Assert.NotNull(package);
        Assert.Equal("file-old", package!.FileId);
        Assert.Equal(hash, package.ZipSha256);
        Assert.Equal(0, env.FileHost.UploadCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task UndeclaredHash_StoresServerComputedHashInRecordAndManifest(string? declaredHash)
    {
        using var env = new SyncTestEnvironment();
        var taskId = await env.SeedRepoAsync();

        var content = SyncTestEnvironment.Content("version-1");
        var computedHash = SyncTestEnvironment.Hash(content);

        env.Handler.MapContent(UrlNew, content);
        env.ServeRepoIndex(env.BuildRepo(
            SyncTestEnvironment.Package(Name, "1.0.0", UrlNew, declaredHash)));

        await env.Synchronizer.StartSync(taskId);

        var package = await env.ReadPackageAsync(Name, "1.0.0");
        Assert.NotNull(package);
        Assert.Equal(computedHash, package!.ZipSha256);
        // The fake file host returns a non-hash file id on purpose: the stored hash must be
        // the one computed from the downloaded content, never the host's file id.
        Assert.NotEqual(computedHash, package.FileId);
        Assert.Equal(await env.FileHost.LookupFileByHashAsync(computedHash), package.FileId);

        var manifestPackage = env.Mapper.Map<VpmPackage>(package);
        Assert.Equal(computedHash, manifestPackage.ZipSha256);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task UndeclaredHash_SecondSync_KeepsTheComputedHash(string? declaredHash)
    {
        using var env = new SyncTestEnvironment();
        await env.SeedRepoAsync();

        var content = SyncTestEnvironment.Content("version-1");
        var computedHash = SyncTestEnvironment.Hash(content);

        env.Handler.MapContent(UrlNew, content);
        env.ServeRepoIndex(env.BuildRepo(
            SyncTestEnvironment.Package(Name, "1.0.0", UrlNew, declaredHash)));

        await env.RunSyncAsync();
        var first = await env.ReadPackageAsync(Name, "1.0.0");
        Assert.NotNull(first);
        Assert.Equal(computedHash, first!.ZipSha256);

        // Second sync hits the already-mirrored package (file host lookup is skipped because the
        // upstream declares no hash) and must keep the hash computed by the first run.
        await env.RunSyncAsync();
        var second = await env.ReadPackageAsync(Name, "1.0.0");
        Assert.NotNull(second);
        Assert.Equal(computedHash, second!.ZipSha256);
        Assert.Equal(first.FileId, second.FileId);
        Assert.Equal(1, env.FileHost.UploadCount);
    }

    [Fact]
    public async Task UndeclaredHash_RecordWithoutKnownHash_StaysWithoutHash()
    {
        using var env = new SyncTestEnvironment();
        await env.SeedRepoAsync();

        await env.SeedMirroredPackageAsync(Name, "1.0.0", null, "file-old");
        env.ServeRepoIndex(env.BuildRepo(
            SyncTestEnvironment.Package(Name, "1.0.0", UrlNew, null)));

        await env.RunSyncAsync();

        // Historical records without a known hash are left untouched: no download, no guessing
        // of the hash from the file id.
        Assert.DoesNotContain(UrlNew, env.Handler.RequestedUrls);

        var package = await env.ReadPackageAsync(Name, "1.0.0");
        Assert.NotNull(package);
        Assert.Null(package!.ZipSha256);
        Assert.Equal("file-old", package.FileId);
    }

    [Fact]
    public async Task NonHashFailure_StillFailsTheRun()
    {
        using var env = new SyncTestEnvironment();
        var taskId = await env.SeedRepoAsync();

        var goodContent = SyncTestEnvironment.Content("version-2");
        var goodHash = SyncTestEnvironment.Hash(goodContent);

        env.Handler.MapStatus(UrlNew, HttpStatusCode.InternalServerError);
        env.Handler.MapContent(UrlSecond, goodContent);
        env.ServeRepoIndex(env.BuildRepo(
            SyncTestEnvironment.Package(Name, "1.0.0", UrlNew, goodHash),
            SyncTestEnvironment.Package(Name, "2.0.0", UrlSecond, goodHash)));

        await env.Synchronizer.StartSync(taskId);

        var task = await env.ReadTaskAsync(taskId);
        Assert.Equal(SyncTaskStatus.Failed, task!.Status);
        Assert.Null(await env.ReadPackageAsync(Name, "2.0.0"));
    }
}
