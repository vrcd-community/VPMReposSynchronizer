# VPM Repos Synchronizer

**English** | [简体中文](README_ZH.md)

A Synchronizer to sync VPM Repos.

Frontend: [vpm-repos-syncronizer-web](https://github.com/vrcd-community/vpm-repos-syncronizer-web)

## Usage

### Dotnet

> If you want to running it in a production environment, you need to setup a service or daemon.

1. Download/Compile the binary files.
2. Configure it, see [Configuration](#configuration).
3. Run `dotnet run VPMReposSynchronizer.Entry.dll` in terminal.

### Docker

Simply run:

```shell
docker run
-p $ANY_PORT_YOU_WANT:8080 misakalinzi/vpm-repos-synchronizer:$VERSION -e $CONFIGURATION \
  --volume=$PATH_TO_WHERE_YOU_WANT_TO_PUT_PACKAGES_FILES:/app/files:rw \
  --volume=$PATH_TO_PACKAGES_DB:/app/packages.db:rw
```

For How to configure it, see [Configuration](#configuration).

## Configuration

### appsettings.json

You can edit the `appsettings.json` file to configure it.

```json
{
  // Synchronizer Configuration
  "Synchronizer": {
    // VPM Repos's url you want to sync. Default is Empty Array.
    "SourceRepoUrls": [
      "https://packages.vrchat.com/official",
      "https://packages.vrchat.com/curated"
    ],
    // Sync Period in Seconds. Default is `3600`.
    "SyncPeriod": 3000
  },
  // LocalFileHostService Configuration (only work when you are using LocalFileHostService)
  "LocalFileHost": {
    // Where to storage the packages files. Default is `files`.
    "FilesPath": "files",
    // The Base Url for LocalFileHostService to get the file uri. Default is `http://example.com`.
    "BaseUrl": "http://localhost:5218/"
    // For Example: The file in physics file system are located at `package-files/example-file` (The `FilesPath` are set to `package-files`)
    // When BaseUrl are set to `https://example.com`, it will return Url `https://example.com/files/example-file`
  },
  // Mirror Repo Meta Data Configuration, see https://vcc.docs.vrchat.com/vpm/repos for more inhumations.
  "MirrorRepoMetaData": {
    "RepoName": "Local Debug VPM Repo",
    "RepoAuthor": "Nameless",
    "RepoUrl": "http://localhost:5218/",
    "RepoId": "local.debug.vpm.repo"
  },
  // File Host Configuration
  "FileHost": {
     // Which FileHostService you want to use, support `LocalFileHost` and `S3FileHost`. Default is `LocalFileHost`.
    "FileHostServiceType": "LocalFileHost"
  }
}

```

### Environment Variables

You may want to use Environment Variables to configure it in some case (like Docker).

Environment variable names reflect the structure of an `appsettings.json` file. Each element in the hierarchy is separated by a double underscore (preferable) or a colon. When the element structure includes an array, the array index should be treated as an additional element name in this path. Consider the `appsettings.json` file and its equivalent values represented as environment variables.

See [Configuration in ASP.NET Core # Naming Of Environment Variables](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/?view=aspnetcore-8.0#naming-of-environment-variables) for more details.

#### A Example in Docker

In this Example:

- Synchronizer will sync with `https://packages.vrchat.com/curated`.
- The LocalFileHostService's BaseUrl is `http://localhost:11451`.
- The Name in MirrorRepo's MetaData  is `DockerTest`.

```bash
docker run -p 11451:8080 misakalinzi/vpm-repos-synchronizer:v0.1.0 \
  -e Synchronizer:SourceRepoUrls:0=https://packages.vrchat.com/curated \
  -e LocalFileHost:BaseUrl=http://localhost:11451/ \
  -e MirrorRepoMetaData:RepoName=DockerTest
```

### Other Ways to Configure it

See [Configuration in ASP.NET Core](https://learn.microsoft.com/zh-cn/aspnet/core/fundamentals/configuration/?view=aspnetcore-8.0).

## License

The software is license under AGPLv3.0, see the `LICENSE.md` file.
