#if !USELIGHTWEIGHTJSONPARSER
using System.Text.Json.Serialization;
#else
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Plugin.Core.Utility.Json;
#endif

// ReSharper disable InconsistentNaming


namespace Hi3Helper.Plugin.Wuwa.Management.Api;

public class WuwaApiResponseGameConfigRef
#if USELIGHTWEIGHTJSONPARSER
    : IJsonElementParsable<WuwaApiResponseGameConfigRef>
      IJsonStreamParsable<WuwaApiResponseGameConfigRef>
#endif
{
#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("indexFile")] // default.config.indexFile
    public string? IndexFile { get; set; }
#endif
#if USELIGHTWEIGHTJSONPARSER
    public static WuwaApiResponseGameConfigRef ParseFrom(Stream stream, bool isDisposeStream = false,
        JsonDocumentOptions options = default)
        => ParseFromAsync(stream, isDisposeStream, options).Result;

    public static async Task<WuwaApiResponseGameConfigRef> ParseFromAsync(Stream stream, bool isDisposeStream = false, JsonDocumentOptions options = default,
        CancellationToken token = default)
    {
        try
        {
            using JsonDocument document = await JsonDocument.ParseAsync(stream, options, token).ConfigureAwait(false);
            return await Task.Factory.StartNew(() => ParseFrom(document.RootElement), token);
        }
        finally
        {
            if (isDisposeStream)
            {
                await stream.DisposeAsync();
            }
        }
    }

    public static WuwaApiResponseGameConfigRef ParseFrom(JsonElement element)
        => new()
        {
            DownloadAssetsReferenceUrl = element.GetString("indexFile")
        };
#endif
}