using System.Net;
using System.Net.Http.Headers;

namespace ServiceLib.Helper;

public static class SubscriptionRequestHeaderHelper
{
    public static Dictionary<string, string> Parse(string? rawHeaders)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (rawHeaders.IsNullOrEmpty())
        {
            return result;
        }

        var lines = rawHeaders
            .Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            var idx = line.IndexOf(':');
            if (idx <= 0 || idx >= line.Length - 1)
            {
                continue;
            }

            var name = line[..idx].Trim();
            var value = line[(idx + 1)..].Trim();
            if (name.IsNullOrEmpty() || value.IsNullOrEmpty())
            {
                continue;
            }

            result[name] = value;
        }

        return result;
    }

    public static bool IsValid(string? rawHeaders)
    {
        if (rawHeaders.IsNullOrEmpty())
        {
            return true;
        }

        return rawHeaders
            .Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(line =>
            {
                var idx = line.IndexOf(':');
                return idx > 0 && idx < line.Length - 1;
            });
    }

    public static string ResolveUserAgent(string? userAgent, IReadOnlyDictionary<string, string> headers, Func<string> defaultUserAgentFactory)
    {
        if (headers.TryGetValue("User-Agent", out var customUserAgent) && customUserAgent.IsNotEmpty())
        {
            return customUserAgent;
        }

        if (userAgent.IsNotEmpty())
        {
            return userAgent ?? string.Empty;
        }

        return defaultUserAgentFactory();
    }

    public static void ApplyHttpHeaders(HttpRequestHeaders requestHeaders, Uri uri, string userAgent, IReadOnlyDictionary<string, string> headers)
    {
        if (userAgent.IsNotEmpty())
        {
            requestHeaders.UserAgent.TryParseAdd(userAgent);
        }

        if (uri.UserInfo.IsNotEmpty())
        {
            requestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Utils.Base64Encode(uri.UserInfo));
        }

        foreach (var (name, value) in headers)
        {
            if (name.Equals("User-Agent", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (name.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
            {
                requestHeaders.Authorization = AuthenticationHeaderValue.Parse(value);
                continue;
            }

            requestHeaders.TryAddWithoutValidation(name, value);
        }
    }

    public static WebHeaderCollection BuildWebHeaders(Uri uri, IReadOnlyDictionary<string, string> headers)
    {
        var webHeaders = new WebHeaderCollection();

        if (uri.UserInfo.IsNotEmpty())
        {
            webHeaders[HttpRequestHeader.Authorization] = "Basic " + Utils.Base64Encode(uri.UserInfo);
        }

        foreach (var (name, value) in headers)
        {
            if (name.Equals("User-Agent", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (name.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
            {
                webHeaders[HttpRequestHeader.Authorization] = value;
                continue;
            }

            webHeaders[name] = value;
        }

        return webHeaders;
    }
}
