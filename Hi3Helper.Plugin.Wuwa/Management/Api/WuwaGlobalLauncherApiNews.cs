using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management.Api;
using Hi3Helper.Plugin.Wuwa.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable InconsistentNaming

#if USELIGHTWEIGHTJSONPARSER
using System.IO;
#else
using System.Text.Json;
#endif

namespace Hi3Helper.Plugin.Wuwa.Management.Api;

[GeneratedComClass]
internal partial class WuwaGlobalLauncherApiNews(string apiResponseBaseUrl, string gameTag, string authenticationHash, string apiOptions, string hash1) : LauncherApiNewsBase
{
    [field: AllowNull, MaybeNull]
    protected override HttpClient ApiResponseHttpClient
    {
        get => field ??= WuwaUtils.CreateApiHttpClient(apiResponseBaseUrl, gameTag, authenticationHash, apiOptions, hash1);
        set;
    }

    [field: AllowNull, MaybeNull]
    protected HttpClient ApiDownloadHttpClient
    {
        get => field ??= WuwaUtils.CreateApiHttpClient(apiResponseBaseUrl, gameTag, authenticationHash, apiOptions, hash1);
        set;
    }

    protected override string ApiResponseBaseUrl { get; } = apiResponseBaseUrl;
    private WuwaApiResponse<WuwaApiResponseSocial>? SocialMediaApiResponse { get; set; }

    protected override async Task<int> InitAsync(CancellationToken token)
    {
        using HttpResponseMessage response = await ApiResponseHttpClient.GetAsync(ApiResponseBaseUrl + "/launcher/G153/50004_obOHXFrFanqsaIEOmuKroCcbZkQRBC7c/social/en.json", HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
#if USELIGHTWEIGHTJSONPARSER
        await using Stream networkStream = await response.Content.ReadAsStreamAsync(token);
        SocialMediaResponse = await WuwaApiResponse<WuwaApiResponseSocial>.ParseFromAsync(networkStream, token: token);
#else
        string jsonResponse = await response.Content.ReadAsStringAsync(token);
        SharedStatic.InstanceLogger.LogTrace("API Social Media and News response: {JsonResponse}", jsonResponse);
        SocialMediaApiResponse = JsonSerializer.Deserialize<WuwaApiResponse<WuwaApiResponseSocial>>(jsonResponse, WuwaApiResponseContext.Default.WuwaApiResponseWuwaApiResponseSocial);
#endif
        SocialMediaApiResponse!.EnsureSuccessCode();
        
        await WuwaIconData.Initialize(token);
        return !response.IsSuccessStatusCode ? (int)response.StatusCode : 0;
    }
    public override void GetNewsEntries(out nint handle, out int count, out bool isDisposable, out bool isAllocated)
        => InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);


    public override void GetCarouselEntries(out nint handle, out int count, out bool isDisposable, out bool isAllocated)
        => InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);

    public override void GetSocialMediaEntries(out nint handle, out int count, out bool isDisposable, out bool isAllocated)
    {
        try
        {
            if (SocialMediaApiResponse?.ResponseData?.SocialMediaEntries is null
                || SocialMediaApiResponse.ResponseData.SocialMediaEntries.Count == 0)
            {
                SharedStatic.InstanceLogger.LogTrace(
                    "[WuwaGlobalLauncherApiNews::GetSocialMediaEntries] API provided no social media entries, returning empty handle.");
                InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);
                return;
            }

            List<WuwaApiResponseSocialResponse> validEntries =
            [
                ..SocialMediaApiResponse.ResponseData.SocialMediaEntries
                    .Where(x => !string.IsNullOrEmpty(x.SocialMediaName) &&
                                !string.IsNullOrEmpty(x.ClickUrl) &&
                                !string.IsNullOrEmpty(x.IconUrl) &&
                                WuwaIconData.EmbeddedDataDictionary.ContainsKey(x.SocialMediaName)
                    )
            ];
            int entryCount = validEntries.Count;
            PluginDisposableMemory<LauncherSocialMediaEntry> memory =
                PluginDisposableMemory<LauncherSocialMediaEntry>.Alloc(entryCount);

            handle = memory.AsSafePointer();
            count = entryCount;
            isDisposable = true;

            SharedStatic.InstanceLogger.LogTrace(
                "[HBRGlobalLauncherApiNews::GetSocialMediaEntries] {EntryCount} entries are allocated at: 0x{Address:x8}",
                entryCount, handle);

            for (int i = 0; i < entryCount; i++)
            {
                string socialMediaName = validEntries[i].SocialMediaName!;
                string clickUrl = validEntries[i].ClickUrl!;
                string? iconUrl = validEntries[i].IconUrl;

                byte[]? iconData = WuwaIconData.GetEmbeddedData(socialMediaName);
                if (iconData is null)
                    continue;

                ref LauncherSocialMediaEntry unmanagedEntries = ref memory[i];
                if (!string.IsNullOrEmpty(iconUrl))
                {
                    unmanagedEntries.WriteQrImage(iconUrl);
                }

                unmanagedEntries.WriteIcon(iconData);
                unmanagedEntries.WriteDescription(socialMediaName);
                unmanagedEntries.WriteClickUrl(clickUrl);
            }

            isAllocated = true;
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogError("Failed to get social media entries: {ErrorMessage}", ex.Message);
            SharedStatic.InstanceLogger.LogDebug(ex, "Exception details: {ExceptionDetails}", ex);
            InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);
        }
    }
    private void InitializeEmpty(out nint handle, out int count, out bool isDisposable, out bool isAllocated)
    {
        handle = nint.Zero;
        count = 0;
        isDisposable = false;
        isAllocated = false;
    }

    public override void Dispose()
    {
        if (IsDisposed)
            return;

        using (ThisInstanceLock.EnterScope())
        {
            ApiDownloadHttpClient.Dispose();
            ApiResponseHttpClient = null!;

            SocialMediaApiResponse = null;
            base.Dispose();
        }
    }
    
}