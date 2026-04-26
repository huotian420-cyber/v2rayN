using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using ServiceLib.Helper;
using Xunit;

namespace ServiceLib.Tests.Helper;

public class SubscriptionSecureHelperTests
{
    [Fact]
    public void ResolveDownloadedContent_WithSecureEnvelope_ShouldDecryptPayload()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var subscriptionUrl = $"https://panel.example.com/panelx/subscriptions/v2r-secure.json?access_token=test#{SubscriptionSecureHelper.SecureSubscriptionKeyFragment}={ToBase64Url(key)}";
        var plainText = "dmxlc3M6Ly8xMjM0";
        var responseBody = BuildEnvelopeJson(key, plainText);

        var resolved = SubscriptionSecureHelper.ResolveDownloadedContent(subscriptionUrl, responseBody);

        resolved.Should().Be(plainText);
    }

    [Fact]
    public void ResolveDownloadedContent_WithoutSecureFragment_ShouldReturnOriginalBody()
    {
        const string responseBody = "{\"version\":\"xray-subscription-sealed-v1\"}";

        var resolved = SubscriptionSecureHelper.ResolveDownloadedContent("https://panel.example.com/subscriptions/v2r.txt", responseBody);

        resolved.Should().Be(responseBody);
    }

    [Fact]
    public void ResolveDownloadedContent_WithWrongKey_ShouldThrow()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var wrongKey = RandomNumberGenerator.GetBytes(32);
        var subscriptionUrl = $"https://panel.example.com/panelx/subscriptions/v2r-secure.json?access_token=test#{SubscriptionSecureHelper.SecureSubscriptionKeyFragment}={ToBase64Url(wrongKey)}";
        var responseBody = BuildEnvelopeJson(key, "payload");

        var action = () => SubscriptionSecureHelper.ResolveDownloadedContent(subscriptionUrl, responseBody);

        action.Should().Throw<InvalidOperationException>().WithMessage("Failed to decrypt secure subscription payload.");
    }

    private static string BuildEnvelopeJson(byte[] key, string plainText)
    {
        using var aesGcm = new AesGcm(key, 16);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = new byte[plainBytes.Length];
        var tagBytes = new byte[16];
        aesGcm.Encrypt(
            nonce,
            plainBytes,
            cipherBytes,
            tagBytes,
            Encoding.UTF8.GetBytes("xray-subscription-sealed-v1")
        );

        var payload = new
        {
            version = "xray-subscription-sealed-v1",
            algorithm = "aes-256-gcm",
            nonce = ToBase64Url(nonce),
            ciphertext = ToBase64Url(cipherBytes.Concat(tagBytes).ToArray()),
        };
        return JsonSerializer.Serialize(payload);
    }

    private static string ToBase64Url(byte[] value)
    {
        return Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
