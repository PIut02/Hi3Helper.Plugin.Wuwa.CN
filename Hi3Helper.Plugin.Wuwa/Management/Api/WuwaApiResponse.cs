using System.Net;
using System.Net.Http;

#if !USELIGHTWEIGHTJSONPARSER
using System.Text.Json.Serialization;
#else
using Hi3Helper.Plugin.Core.Utility.Json;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
#endif

namespace Hi3Helper.Plugin.Wuwa.Management.Api;

#if !USELIGHTWEIGHTJSONPARSER
[JsonSerializable(typeof(WuwaApiResponse<WuwaApiResponseMedia>))]
[JsonSerializable(typeof(WuwaApiResponse<WuwaApiResponseSocial>))]
// [JsonSerializable(typeof(WuwaApiResponse<WuwaApiResponseGameConfig>))]
// [JsonSerializable(typeof(WuwaApiResponse<WuwaApiResponseGameConfigRef>))]
public partial class WuwaApiResponseContext : JsonSerializerContext;
#endif

public class WuwaApiResponse<T>
#if USELIGHTWEIGHTJSONPARSER
    : IJsonElementParsable<WuwaApiResponse<T>>,
      IJsonStreamParsable<WuwaApiResponse<T>>
    where T : IJsonElementParsable<T>, new()
#endif
{
#if !USELIGHTWEIGHTJSONPARSER
    [JsonPropertyName("data")]
#endif
    public int ReturnCode { get; set; }
    public T? ResponseData { get; set; }
    
    public void EnsureSuccessCode()
    {
        if (ResponseData == null || ReturnCode != 200) 
        {
            throw new HttpRequestException($"API returned unsuccessful code: {ReturnCode}", null, (HttpStatusCode)ReturnCode);
        }
        {
            throw new HttpRequestException("API returned no data", null, HttpStatusCode.InternalServerError);
        }
    }

#if USELIGHTWEIGHTJSONPARSER
    public static WuwaApiResponse<T> ParseFrom(Stream stream, bool isDisposeStream = false, JsonDocumentOptions options = default)
        => ParseFromAsync(stream, isDisposeStream, options).Result;

    public static async Task<WuwaApiResponse<T>> ParseFromAsync(Stream stream, bool isDisposeStream = false, JsonDocumentOptions options = default,
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

    public static WuwaApiResponse<T> ParseFrom(JsonElement rootElement)
    {
        T? innerValue = default;
        if (rootElement.TryGetProperty("data", out JsonElement dataElement))
        {
            innerValue = T.ParseFrom(dataElement);
        }

        return new WuwaApiResponse<T>
        {
            ResponseData = innerValue
        };
    }
#endif
}

