using System;
using System.Buffers;
using System.Buffers.Text;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Utility;
using Microsoft.Extensions.Logging;

// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo

namespace Hi3Helper.Plugin.Wuwa.CN.Utils;

internal static class WuwaUtils
{
    internal static HttpClient CreateApiHttpClient(string? apiBaseUrl = null, string? gameTag = null,
        string? authCdnToken = "", string? apiOptions = "", string? hash1 = "")
    {
        return CreateApiHttpClientBuilder(apiBaseUrl, gameTag, authCdnToken, apiOptions, hash1).Create();
    }

    private static PluginHttpClientBuilder CreateApiHttpClientBuilder(string? apiBaseUrl, string? gameTag = null,
        string? authCdnToken = "", string? accessOption = null, string? hash1 = "")
    {
        var builder = new PluginHttpClientBuilder()
            .SetUserAgent(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/138.0.0.0 Safari/537.36");

        if (authCdnToken == null)
            throw new ArgumentNullException(nameof(authCdnToken),
                "authCdnToken cannot be empty. Use string.Empty if you want to ignore it instead.");

        if (!string.IsNullOrEmpty(authCdnToken))
        {
            authCdnToken = authCdnToken.AeonPlsHelpMe();
#if DEBUG
            SharedStatic.InstanceLogger.LogTrace("Decoded authCdnToken: {}", authCdnToken);
#endif
        }

        switch (accessOption)
        {
            case "news":
                builder.SetBaseUrl(apiBaseUrl.CombineUrlFromString("launcher", authCdnToken, gameTag, "information",
                    "en.json"));
                break;
            case "bg":
                builder.SetBaseUrl(apiBaseUrl.CombineUrlFromString("launcher", authCdnToken, gameTag, "background",
                    hash1, "en.json"));
                break;
            case "media":
                builder.SetBaseUrl(apiBaseUrl.CombineUrlFromString("launcher", gameTag, authCdnToken, "social",
                    "en.json"));
                break;
        }

#if DEBUG
        SharedStatic.InstanceLogger.LogTrace("Created HttpClient with Token: {}", authCdnToken);
#endif

        return builder;
    }

    internal static string AeonPlsHelpMe(this string whatDaDup)
    {
        const int amountOfBeggingForHelp = 4096;

        WuwaTransform transform = new(99);
        var bufferSize = Encoding.UTF8.GetMaxByteCount(whatDaDup.Length);

        var iWannaConvene = bufferSize <= amountOfBeggingForHelp
            ? null
            : ArrayPool<byte>.Shared.Rent(bufferSize);

        scoped var wannaConvene = iWannaConvene ?? stackalloc byte[bufferSize];
        string resultString;
        try
        {
            var isAsterite2Sufficient =
                Encoding.UTF8.TryGetBytes(whatDaDup, wannaConvene, out var amountOfCryFromBegging);
#if DEBUG
            SharedStatic.InstanceLogger.LogDebug(
                "[WuwaUtils::AeonPlsHelpMe] Attempting to decode string using AeonPlsHelpMe. Input: {Input}, BufferSize: {BufferSize}, IsBufferSufficient: {IsBufferSufficient}, EncodedLength: {EncodedLength}",
                whatDaDup, wannaConvene.Length, isAsterite2Sufficient, amountOfCryFromBegging);
#endif

            // Try Base64Url decode in-place. If decode returns 0 it means input wasn't Base64Url encoded.
            var decodedLen = 0;
            try
            {
                decodedLen = Base64Url.DecodeFromUtf8InPlace(wannaConvene[..amountOfCryFromBegging]);
            }
            catch
            {
                decodedLen = 0;
            }

            if (!isAsterite2Sufficient)
            {
                resultString = whatDaDup;
            }
            else if (decodedLen == 0)
            {
                resultString = whatDaDup;
            }
            else
            {
                var transformedLen = transform.TransformBlockCore(wannaConvene[..decodedLen], wannaConvene);
                resultString = Encoding.UTF8.GetString(wannaConvene[..transformedLen]);
            }
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogError("A string decoding error occurred: {Exception}", ex);
            resultString = whatDaDup;
        }
        finally
        {
            if (iWannaConvene != null) ArrayPool<byte>.Shared.Return(iWannaConvene);
        }

        var beforeSanitize = resultString;
        var sanitized = resultString.Replace("\r", "").Replace("\n", "").Trim();

        if (sanitized.Length > 0)
        {
            var sspan = sanitized.AsSpan();
            var buf = new char[sspan.Length];
            var di = 0;
            for (var i = 0; i < sspan.Length; i++)
                if (!char.IsWhiteSpace(sspan[i]))
                    buf[di++] = sspan[i];

            if (di != sanitized.Length)
            {
                var removedWs = new string(buf, 0, di);
                sanitized = removedWs;
            }
        }

        if (sanitized.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            sanitized = sanitized["http://".Length..];
        else if (sanitized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            sanitized = sanitized["https://".Length..];

        sanitized = sanitized.TrimEnd('/');

        return sanitized;
    }

    internal static string ComputeMd5Hex(Stream stream, CancellationToken token = default)
    {
        stream.Seek(0, SeekOrigin.Begin);
        using var md5 = MD5.Create();

        var buffer = ArrayPool<byte>.Shared.Rent(64 << 10);
        try
        {
            int bytesRead;
            while ((bytesRead = stream.Read(buffer)) > 0) md5.TransformBlock(buffer, 0, bytesRead, null, 0);
            md5.TransformFinalBlock(buffer, 0, 0);

            var hash = md5.Hash!;
            return Convert.ToHexStringLower(hash);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }


    internal static async ValueTask<string> ComputeMd5HexAsync(Stream stream, CancellationToken token = default)
    {
        stream.Seek(0, SeekOrigin.Begin);
        using var md5 = MD5.Create();

        var buffer = ArrayPool<byte>.Shared.Rent(64 << 10);
        try
        {
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
                md5.TransformBlock(buffer, 0, bytesRead, null, 0);
            md5.TransformFinalBlock(buffer, 0, 0);

            var hash = md5.Hash!;
            return Convert.ToHexStringLower(hash);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}