using System.Reflection;
using System.Runtime.InteropServices;
using VPMReposSynchronizer.Core.Models.Types;

namespace VPMReposSynchronizer.Core.Utils;

public static class BuildInfoUtils
{
    public static BuildInfo GetBuildInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var commitHashAttribute = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "GitCommitHash");
        var branchNameAttribute = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "GitBranchName");
        var buildTimeAttribute = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "BuildTime");

        var buildTime = DateTimeOffset.TryParse(buildTimeAttribute?.Value, out var parsedBuildTime)
            ? parsedBuildTime
            : DateTimeOffset.MinValue;

        var commitHash = commitHashAttribute?.Value ?? "unknown";
        var branchName = branchNameAttribute?.Value ?? "unknown";

        return new BuildInfo(
            assembly.GetName().Version?.ToString(),
            RuntimeInformation.OSArchitecture.ToString(),
            buildTime,
            commitHash,
            branchName
        );
    }
}