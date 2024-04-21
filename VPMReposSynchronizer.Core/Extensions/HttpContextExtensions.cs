using Microsoft.AspNetCore.Http;

namespace VPMReposSynchronizer.Core.Extensions;

public static class HttpContextExtensions
{
    public static string GetIpAddress(this HttpContext httpContext)
    {
        if (httpContext.Request.Headers["CF-CONNECTING-IP"].ToString() is { } cloudflareConnectingIp && !string.IsNullOrEmpty(cloudflareConnectingIp))
        {
            return cloudflareConnectingIp;
        }

        var ipAddress = httpContext.Request.HttpContext.GetServerVariable("HTTP_X_FORWARDED_FOR");
        var connectionIpAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (string.IsNullOrEmpty(ipAddress))
        {
            return connectionIpAddress;
        }

        var addresses = ipAddress.Split(',');

        return addresses.Length != 0 ? addresses[^1] : connectionIpAddress;
    }
}