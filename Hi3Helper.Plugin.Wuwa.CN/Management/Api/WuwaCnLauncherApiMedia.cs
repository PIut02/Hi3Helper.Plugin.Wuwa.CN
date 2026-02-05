using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices.Marshalling;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management.Api;
using Hi3Helper.Plugin.Core.Utility;
using Microsoft.Extensions.Logging;
namespace Hi3Helper.Plugin.Wuwa.CN.Management.Api;

[GeneratedComClass]
internal partial class WuwaCnLauncherApiMedia : LauncherApiMediaBase
{
    internal WuwaCnLauncherApiMedia(string apiResponseBaseUrl, string gameTag, string authenticationHash, string hash1)
    {
        ApiResponseBaseUrl = apiResponseBaseUrl;
        GameTag = gameTag;
        AuthenticationHash = authenticationHash;
        // Hash1 = hash1; // 国服忽略此参数
    }

    private string ApiResponseBaseUrl { get; }
    private string GameTag { get; }
    private string AuthenticationHash { get; }

    // Hash1 在国服不再从 Preset 获取，而是动态从 Index.json 获取
    // private string Hash1 { get; } 

    private WuwaApiResponseMedia? ApiResponse { get; set; }

    [field: AllowNull]
    [field: MaybeNull]
    protected HttpClient ApiDownloadHttpClient
    {
        get => field ??= new PluginHttpClientBuilder()
            .SetAllowedDecompression(DecompressionMethods.None)
            .AllowCookies()
            .AllowRedirections()
            .AllowUntrustedCert()
            .Create();
        set;
    }

    protected override HttpClient? ApiResponseHttpClient
    {
        get => field ??= new PluginHttpClientBuilder()
            .SetAllowedDecompression(DecompressionMethods.All)
            .AllowCookies()
            .AllowRedirections()
            .AllowUntrustedCert()
            .Create();
        set;
    }

    protected override async Task<int> InitAsync(CancellationToken token)
    {
        var finalTag = "G152";
        var finalAuth = "10003_Y8xXrXk65DqFHEDgApn3cpK5lfczpFx5";
        
        // URL: /launcher/launcher/{Key}/{ID}/index.json
        var indexUrl = $"{ApiResponseBaseUrl}launcher/launcher/{finalAuth}/{finalTag}/index.json";

        var backgroundHash = "ucLkUF6LaT5NqcUzIoLQIUu3ALrdU2qL"; // 默认后备值

        try
        {
            SharedStatic.InstanceLogger.LogDebug(
                $"[WuwaCnLauncherApiMedia] Fetching Index for Background Hash: {indexUrl}");
            var indexJson = await ApiResponseHttpClient!.GetStringAsync(indexUrl, token);
            
            var rootNode = JsonNode.Parse(indexJson);

            // functionCode -> background
            var fetchedHash = rootNode?["functionCode"]?["background"]?.GetValue<string>();

            if (!string.IsNullOrEmpty(fetchedHash))
            {
                backgroundHash = fetchedHash;
                SharedStatic.InstanceLogger.LogDebug(
                    $"[WuwaCnLauncherApiMedia] Found dynamic background hash: {backgroundHash}");
            }
            else
            {
                SharedStatic.InstanceLogger.LogWarning(
                    "[WuwaCnLauncherApiMedia] Could not find 'functionCode.background' in index.json. Using fallback.");
            }
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogWarning(
                $"[WuwaCnLauncherApiMedia] Failed to fetch index.json for background hash: {ex.Message}. Using fallback.");
        }

        // URL: /launcher/{Key}/{ID}/background/{Hash}/zh-Hans.json
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var mediaUrl = $"{ApiResponseBaseUrl}launcher/{finalAuth}/{finalTag}/background/{backgroundHash}/zh-Hans.json?_t={timestamp}";

        SharedStatic.InstanceLogger.LogDebug($"[WuwaCnLauncherApiMedia] Requesting Media Config: {mediaUrl}");

        using var response = await ApiResponseHttpClient!.GetAsync(mediaUrl, token);
        if (!response.IsSuccessStatusCode)
        {
            SharedStatic.InstanceLogger.LogWarning(
                $"[WuwaCnLauncherApiMedia] Media request failed: {response.StatusCode}");
            return 0;
        }

        var jsonResponse = await response.Content.ReadAsStringAsync(token);
        ApiResponse =
            JsonSerializer.Deserialize<WuwaApiResponseMedia>(jsonResponse,
                WuwaApiResponseContext.Default.WuwaApiResponseMedia);

        return ApiResponse == null ? 0 : 1;
    }

    public override void GetBackgroundEntries(out nint handle, out int count, out bool isDisposable,
        out bool isAllocated)
    {
        using (ThisInstanceLock.EnterScope())
        {
            var backgroundEntries = PluginDisposableMemory<LauncherPathEntry>.Alloc();
            try
            {
                ref var entry = ref backgroundEntries[0];
                if (ApiResponse == null || string.IsNullOrEmpty(ApiResponse.BackgroundImageUrl))
                {
                    isDisposable = false;
                    handle = nint.Zero;
                    count = 0;
                    isAllocated = false;
                    return;
                }

                entry.Write(ApiResponse.BackgroundImageUrl, Span<byte>.Empty);
                isAllocated = true;
            }
            finally
            {
                isDisposable = backgroundEntries.IsDisposable == 1;
                handle = backgroundEntries.AsSafePointer();
                count = backgroundEntries.Length;
            }
        }
    }

    public override void GetBackgroundFlag(out LauncherBackgroundFlag result)
    {
        result = LauncherBackgroundFlag.TypeIsVideo | LauncherBackgroundFlag.TypeIsImage;
    }

    public override void GetLogoFlag(out LauncherBackgroundFlag result)
    {
        result = LauncherBackgroundFlag.None;
    }

    public override void GetLogoOverlayEntries(out nint handle, out int count, out bool isDisposable,
        out bool isAllocated)
    {
        isDisposable = false;
        handle = nint.Zero;
        count = 0;
        isAllocated = false;
    }

    protected override async Task DownloadAssetAsyncInner(HttpClient? client, string fileUrl, Stream outputStream,
        PluginDisposableMemory<byte> fileChecksum, PluginFiles.FileReadProgressDelegate? downloadProgress,
        CancellationToken token)
    {
        await base.DownloadAssetAsyncInner(ApiDownloadHttpClient, fileUrl, outputStream, fileChecksum, downloadProgress,
            token);
    }

    public override void Dispose()
    {
        if (IsDisposed) return;
        using (ThisInstanceLock.EnterScope())
        {
            ApiResponseHttpClient?.Dispose();
            ApiDownloadHttpClient?.Dispose();
            ApiResponse = null;
            base.Dispose();
        }
    }
}