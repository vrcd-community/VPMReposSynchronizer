using System.Security.Cryptography;

namespace VPMReposSynchronizer.Core.Utils;

public static class FileUtils
{
    public static async ValueTask<string> HashFile(string path)
    {
        await using var fileStream = File.OpenRead(path);
        return await HashStream(fileStream);
    }

    public static async ValueTask<string> HashStream(Stream stream)
    {
        return Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLower();
    }
}