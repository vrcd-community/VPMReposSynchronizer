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

        if (!string.IsNullOrEmpty(ipAddress))
        {
            var addresses = ipAddress.Split(',');
            if (addresses.Length != 0)
                return addresses[^1];
        }

        return httpContext.Connection.RemoteIpAddress.ToString();
    }
}