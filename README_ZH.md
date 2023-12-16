# VPM Repos Synchronizer

[English](README.md) | **简体中文**

用于同步 VPM 仓库的同步器。

## 使用方法

### Dotnet

> 如果您希望在生产环境中运行它，您需要设置一个服务或守护程序。

1. 下载/编译二进制文件。
2. 配置它，请参阅[配置](#配置)。
3. 在终端运行 `dotnet run VPMReposSynchronizer.Entry.dll`。

### Docker

只需运行：

```shell
docker run
-p $ANY_PORT_YOU_WANT:8080 misakalinzi/vpm-repos-synchronizer:$VERSION -e $CONFIGURATION \
  --volume=$PATH_TO_WHERE_YOU_WANT_TO_PUT_PACKAGES_FILES:/app/files:rw \
  --volume=$PATH_TO_PACKAGES_DB:/app/packages.db:rw
```

有关如何配置，请参阅[配置](#配置)。

## 配置

### appsettings.json

您可以编辑 `appsettings.json` 文件来配置它。

```json
{
  // 同步器配置
  "Synchronizer": {
    // 要同步的 VPM 仓库的URL。默认为空数组。
    "SourceRepoUrls": [
      "https://packages.vrchat.com/official",
      "https://packages.vrchat.com/curated"
    ],
    // 同步周期（秒）。默认为 `3600`。
    "SyncPeriod": 3000
  },
  // 本地文件主机服务配置（仅在使用 LocalFileHostService 时有效）
  "LocalFileHost": {
    // 存储软件包文件的位置。默认为 `files`。
    "FilesPath": "files",
    // 用于 LocalFileHostService 获取文件 URI 的基本URL。默认为 `http://example.com`。
    "BaseUrl": "http://localhost:5218/"
    // 例如：物理文件系统中的文件位于 `package-files/example-file`（`FilesPath` 设置为 `package-files`）
    // 当 BaseUrl 设置为 `https://example.com` 时，它将返回 URL `https://example.com/files/example-file`
  },
  // 镜像仓库元数据配置，请参见 https://vcc.docs.vrchat.com/vpm/repos 以获取更多信息。
  "MirrorRepoMetaData": {
    "RepoName": "Local Debug VPM Repo",
    "RepoAuthor": "Nameless",
    "RepoUrl": "http://localhost:5218/",
    "RepoId": "local.debug.vpm.repo"
  },
  // 文件主机配置
  "FileHost": {
     // 您想要使用的 FileHostService，支持 `LocalFileHost` 和 `S3FileHost`。默认为 `LocalFileHost`。
    "FileHostServiceType": "LocalFileHost"
  }
}
```

### 环境变量

您可能希望在某些情况下（例如 Docker 中）使用环境变量进行配置。

环境变量的名称反映了 `appsettings.json` 文件的结构。层次结构中的每个元素由双下划线（最好）或冒号分隔。当元素结构包含数组时，应将数组索引视为此路径中的附加元素名称。请参考 `appsettings.json` 文件及其等效配置文件的作为环境变量表示的值。

有关更多详细信息，请参阅 [ASP.NET Core 中的配置 # 环境变量的命名](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/?view=aspnetcore-8.0#naming-of-environment-variables)。

#### Docker 中的示例

在此示例中：

- 同步器将与 `https://packages.vrchat.com/curated` 同步。
- LocalFileHostService的BaseUrl为 `http://localhost:11451`。
- MirrorRepo的MetaData中的名称为 `DockerTest`。

```bash
docker run -p 11451:8080 misakalinzi/vpm-repos-synchronizer:v0.1.0 \
  -e Synchronizer:SourceRepoUrls:0=https://packages.vrchat.com/curated \
  -e LocalFileHost:BaseUrl=http://localhost:11451/ \
  -e MirrorRepoMetaData:RepoName=DockerTest
```

### 其他配置方式

请参阅 [ASP.NET Core 中的配置](https://learn.microsoft.com/zh-cn/aspnet/core/fundamentals/configuration/?view=aspnetcore-8.0)。

## 许可证

该软件在 AGPLv3.0 许可证发布，详见 `LICENSE.md` 文件。
