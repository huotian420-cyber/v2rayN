using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ServiceLib.Helper;

public static class SubscriptionSecureHelper
{
    public const string SecureSubscriptionKeyFragment = "xray-sub-key";

    private const string SecureSubscriptionVersion = "xray-subscription-sealed-v1";
    private const string SecureSubscriptionAlgorithm = "aes-256-gcm";
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class SecureSubscriptionEnvelope
    {
        public string? Version { get; set; }

        public string? Algorithm { get; set; }

        public string? Nonce { get; set; }

        public string? Ciphertext { get; set; }
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

        if (envelope.Nonce.IsNullOrEmpty() || envelope.Ciphertext.IsNullOrEmpty())
        {
            throw new InvalidOperationException("Secure subscription payload is incomplete.");
        }

        var nonce = DecodeBase64Url(envelope.Nonce);
        var cipherPayload = DecodeBase64Url(envelope.Ciphertext);
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

    private static bool TryResolveFragmentKey(string subscriptionUrl, out byte[] key)
    {
        key = [];

        if (subscriptionUrl.IsNullOrEmpty())
        {
            return false;
        }

        var uri = Utils.TryUri(subscriptionUrl);
        if (uri == null || uri.Fragment.IsNullOrEmpty())
        {
            return false;
        }

        var rawFragment = uri.Fragment.TrimStart('#');
        foreach (var segment in rawFragment.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = segment.Split('=', 2);
            var name = Uri.UnescapeDataString(parts[0]);
            if (!string.Equals(name, SecureSubscriptionKeyFragment, StringComparison.Ordinal))
            {
                continue;
            }

            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            if (value.IsNullOrEmpty())
            {
                return false;
            }

            key = DecodeBase64Url(value);
            return key.Length > 0;
        }

        return false;
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
