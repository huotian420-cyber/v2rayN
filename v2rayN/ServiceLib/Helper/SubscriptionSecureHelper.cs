using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ServiceLib.Helper;

public static class SubscriptionSecureHelper
{
    public const string SecureSubscriptionKeyFragment = "xray-sub-key";

    private const string SecureSubscriptionVersion = "xray-subscription-sealed-v1";
    private const string SecureSubscriptionAlgorithm = "aes-256-gcm";
    private const string GenericSecureSubscriptionKeyName = "key";
    private static readonly HashSet<string> SecureSubscriptionKeyNames = new(StringComparer.Ordinal)
    {
        SecureSubscriptionKeyFragment,
        GenericSecureSubscriptionKeyName,
        "sub_key",
        "subscription_key"
    };
    private static readonly HashSet<string> SecureSubscriptionDownloadKeyNames = new(StringComparer.Ordinal)
    {
        SecureSubscriptionKeyFragment,
        "sub_key",
        "subscription_key"
    };
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class SecureSubscriptionEnvelope
    {
        public string? Version { get; set; }

        public string? Algorithm { get; set; }

        public string? Nonce { get; set; }

        public string? Iv { get; set; }

        public string? Ciphertext { get; set; }

        public string? Data { get; set; }

        public string? Payload { get; set; }
    }

    public static string ResolveDownloadedContent(string subscriptionUrl, string? responseContent)
    {
        var body = responseContent.TrimEx();
        if (body.IsNullOrEmpty())
        {
            return string.Empty;
        }

        if (!TryResolveFragmentKey(subscriptionUrl, out var key))
        {
            return body;
        }

        if (!TryParseEnvelope(body, out var envelope))
        {
            return body;
        }

        if (!string.Equals(envelope.Version, SecureSubscriptionVersion, StringComparison.Ordinal) ||
            !string.Equals(envelope.Algorithm, SecureSubscriptionAlgorithm, StringComparison.Ordinal))
        {
            return body;
        }

        if (key.Length != 32)
        {
            throw new InvalidOperationException("Secure subscription key must be 32 bytes.");
        }

        var nonceValue = envelope.Nonce ?? envelope.Iv;
        var cipherValue = envelope.Ciphertext ?? envelope.Data ?? envelope.Payload;
        if (nonceValue.IsNullOrEmpty() || cipherValue.IsNullOrEmpty())
        {
            throw new InvalidOperationException("Secure subscription payload is incomplete.");
        }

        var nonce = DecodeBase64Url(nonceValue);
        var cipherPayload = DecodeBase64Url(cipherValue);
        if (cipherPayload.Length <= 16)
        {
            throw new InvalidOperationException("Secure subscription payload is invalid.");
        }

        var cipherText = cipherPayload.AsSpan(0, cipherPayload.Length - 16).ToArray();
        var tag = cipherPayload.AsSpan(cipherPayload.Length - 16, 16).ToArray();
        var plainBytes = new byte[cipherText.Length];

        try
        {
            using var aesGcm = new AesGcm(key, 16);
            aesGcm.Decrypt(
                nonce,
                cipherText,
                tag,
                plainBytes,
                Encoding.UTF8.GetBytes(SecureSubscriptionVersion)
            );
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException("Failed to decrypt secure subscription payload.", ex);
        }

        return Encoding.UTF8.GetString(plainBytes).Trim();
    }

    public static string ToDownloadUrl(string subscriptionUrl)
    {
        var trimmed = subscriptionUrl.TrimEx();
        if (trimmed.IsNullOrEmpty())
        {
            return string.Empty;
        }

        var withoutFragment = trimmed.Split('#', 2)[0];
        return RemoveSecureKeyQueryParameters(withoutFragment);
    }

    public static bool HasSecureSubscriptionKey(string subscriptionUrl)
    {
        return TryResolveFragmentKey(subscriptionUrl, out _);
    }

    private static bool TryResolveFragmentKey(string subscriptionUrl, out byte[] key)
    {
        key = [];

        if (subscriptionUrl.IsNullOrEmpty())
        {
            return false;
        }

        var uri = Utils.TryUri(subscriptionUrl);
        if (uri == null)
        {
            return false;
        }
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return TryResolveKeyFromParameterText(uri.Fragment.TrimStart('#'), out key)
            || TryResolveKeyFromParameterText(uri.Query.TrimStart('?'), out key);
    }

    private static bool TryResolveKeyFromParameterText(string raw, out byte[] key)
    {
        key = [];
        if (raw.IsNullOrEmpty())
        {
            return false;
        }

        foreach (var segment in raw.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = segment.Split('=', 2);
            var name = Uri.UnescapeDataString(parts[0]);
            if (!SecureSubscriptionKeyNames.Contains(name))
            {
                continue;
            }

            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            if (value.IsNullOrEmpty())
            {
                return false;
            }

            try
            {
                key = DecodeBase64Url(value);
            }
            catch (FormatException)
            {
                continue;
            }

            if (key.Length != 32)
            {
                key = [];
                continue;
            }

            return true;
        }

        return false;
    }

    private static string RemoveSecureKeyQueryParameters(string url)
    {
        var queryIndex = url.IndexOf('?');
        if (queryIndex < 0)
        {
            return url;
        }

        var baseUrl = url[..queryIndex];
        var query = url[(queryIndex + 1)..];
        if (query.IsNullOrEmpty())
        {
            return baseUrl;
        }

        var keptParameters = query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(segment =>
            {
                var name = segment.Split('=', 2)[0];
                try
                {
                    return !SecureSubscriptionDownloadKeyNames.Contains(Uri.UnescapeDataString(name));
                }
                catch
                {
                    return true;
                }
            })
            .ToArray();

        return keptParameters.Length == 0 ? baseUrl : $"{baseUrl}?{string.Join("&", keptParameters)}";
    }

    private static bool TryParseEnvelope(string body, out SecureSubscriptionEnvelope envelope)
    {
        envelope = new SecureSubscriptionEnvelope();
        if (!body.TrimStart().StartsWith("{", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<SecureSubscriptionEnvelope>(body, _jsonSerializerOptions);
            if (parsed == null)
            {
                return false;
            }

            envelope = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var normalized = value.Trim()
            .Replace('-', '+')
            .Replace('_', '/');

        if (normalized.Length % 4 != 0)
        {
            normalized = normalized.PadRight(normalized.Length + 4 - (normalized.Length % 4), '=');
        }

        return Convert.FromBase64String(normalized);
    }
}
