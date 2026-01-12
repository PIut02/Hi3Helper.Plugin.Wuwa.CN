using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Management.Api;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.Plugin.Core.Utility.Json;
using Microsoft.Extensions.Logging;

namespace Hi3Helper.Plugin.Wuwa.CN.Management.Api;

[GeneratedComClass]
internal partial class WuwaCnLauncherApiNews(string apiResponseBaseUrl, string gameTag, string authenticationHash)
    : LauncherApiNewsBase
{
    [field: AllowNull]
    [field: MaybeNull]
    protected override HttpClient ApiResponseHttpClient
    {
        get => field ??= new PluginHttpClientBuilder()
            .SetAllowedDecompression(DecompressionMethods.GZip | DecompressionMethods.Deflate)
            .AllowCookies()
            .AllowRedirections()
            .AllowUntrustedCert()
            .Create();
        set;
    }

    [field: AllowNull]
    [field: MaybeNull]
    protected HttpClient ApiDownloadHttpClient
    {
        get => field ??= new PluginHttpClientBuilder()
            .SetAllowedDecompression(DecompressionMethods.GZip)
            .AllowCookies()
            .AllowRedirections()
            .AllowUntrustedCert()
            .Create();
        set;
    }

    protected override string ApiResponseBaseUrl { get; } = apiResponseBaseUrl;

    private WuwaApiResponseSocial? ApiResponseSocialMedia { get; set; }
    private WuwaApiResponseNews? ApiResponseNewsAndCarousel { get; set; }

    protected override async Task<int> InitAsync(CancellationToken token)
    {
        var decryptedTag = "G152";
        var decryptedAuth = "10003_Y8xXrXk65DqFHEDgApn3cpK5lfczpFx5";
        
        var requestNewsUrl = ApiResponseBaseUrl
            .CombineUrlFromString("launcher", decryptedAuth, decryptedTag, "information", "zh-Hans.json");

        var requestSocialUrl = ApiResponseBaseUrl
            .CombineUrlFromString("pcstarter", "prod", "game", decryptedTag, decryptedAuth, "social", "zh-Hans.json");

        try
        {
            ApiResponseNewsAndCarousel = await ApiResponseHttpClient
                .GetApiResponseFromJsonAsync(requestNewsUrl, WuwaApiResponseContext.Default.WuwaApiResponseNews, token);
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogWarning($"[WuwaCnLauncherApiNews] Failed to load news: {ex.Message}");
        }

        try
        {
            ApiResponseSocialMedia = await ApiResponseHttpClient
                .GetApiResponseFromJsonAsync(requestSocialUrl, WuwaApiResponseContext.Default.WuwaApiResponseSocial,
                    token);
        }
        catch
        {
            /* Ignore social media errors */
        }

        return 0;
    }

    public override void GetNewsEntries(out nint handle, out int count, out bool isDisposable, out bool isAllocated)
    {
        if (ApiResponseNewsAndCarousel?.NewsData == null)
        {
            InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);
            return;
        }

        var entryEventCount = ApiResponseNewsAndCarousel.NewsData.ContentKindEvent?.Contents?.Length ?? 0;
        var entryNewsCount = ApiResponseNewsAndCarousel.NewsData.ContentKindNews?.Contents?.Length ?? 0;
        var entryNoticeCount = ApiResponseNewsAndCarousel.NewsData.ContentKindNotice?.Contents?.Length ?? 0;

        count = entryEventCount + entryNewsCount + entryNoticeCount;

        if (count == 0)
        {
            InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);
            return;
        }

        var memory = PluginDisposableMemory<LauncherNewsEntry>.Alloc(count);
        handle = memory.AsSafePointer();
        isDisposable = true;
        isAllocated = true;

        var memIndex = 0;
        if (entryEventCount > 0)
            Write(ApiResponseNewsAndCarousel.NewsData.ContentKindEvent!.Contents, ref memory, ref memIndex,
                LauncherNewsEntryType.Event);
        if (entryNewsCount > 0)
            Write(ApiResponseNewsAndCarousel.NewsData.ContentKindNews!.Contents, ref memory, ref memIndex,
                LauncherNewsEntryType.Info);
        if (entryNoticeCount > 0)
            Write(ApiResponseNewsAndCarousel.NewsData.ContentKindNotice!.Contents, ref memory, ref memIndex,
                LauncherNewsEntryType.Notice);

        static void Write(Span<WuwaApiResponseNewsEntry> entriesSpan, ref PluginDisposableMemory<LauncherNewsEntry> mem,
            ref int memOffset, LauncherNewsEntryType type)
        {
            for (var i = 0; i < entriesSpan.Length; i++, memOffset++)
            {
                ref var unmanagedEntry = ref mem[memOffset];
                var entry = entriesSpan[i];
                unmanagedEntry.Write(entry.NewsTitle, null, entry.ClickUrl, entry.Date, type);
            }
        }
    }

    public override void GetCarouselEntries(out nint handle, out int count, out bool isDisposable, out bool isAllocated)
    {
        if (ApiResponseNewsAndCarousel == null || ApiResponseNewsAndCarousel.CarouselData == null)
        {
            InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);
            return;
        }

        count = ApiResponseNewsAndCarousel.CarouselData.Length;
        if (count == 0)
        {
            InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);
            return;
        }

        var memory =
            PluginDisposableMemory<LauncherCarouselEntry>.Alloc(count);
        handle = memory.AsSafePointer();
        isDisposable = true;
        isAllocated = true;

        Span<WuwaApiResponseCarouselEntry> entries = ApiResponseNewsAndCarousel.CarouselData;
        for (var i = 0; i < count; i++)
        {
            ref var unmanagedEntry = ref memory[i];
            unmanagedEntry.Write(entries[i].Description, entries[i].ImageUrl, entries[i].ClickUrl);
        }
    }

    public override void GetSocialMediaEntries(out nint handle, out int count, out bool isDisposable,
        out bool isAllocated)
    {
        try
        {
            if (ApiResponseSocialMedia?.SocialMediaEntries is null ||
                ApiResponseSocialMedia.SocialMediaEntries.Count == 0)
            {
                InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);
                return;
            }

            List<WuwaApiResponseSocialResponse> validEntries =
            [
                ..ApiResponseSocialMedia.SocialMediaEntries
                    .Where(x => !string.IsNullOrEmpty(x.SocialMediaName) &&
                                !string.IsNullOrEmpty(x.ClickUrl) &&
                                !string.IsNullOrEmpty(x.IconUrl))
            ];

            var entryCount = validEntries.Count;
            if (entryCount == 0)
            {
                InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);
                return;
            }

            var memory =
                PluginDisposableMemory<LauncherSocialMediaEntry>.Alloc(entryCount);
            handle = memory.AsSafePointer();
            count = entryCount;
            isDisposable = true;
            isAllocated = true;

            for (var i = 0; i < entryCount; i++)
            {
                ref var unmanagedEntries = ref memory[i];
                var entry = validEntries[i];
                unmanagedEntries.WriteIcon(entry.IconUrl);
                unmanagedEntries.WriteDescription(entry.SocialMediaName!);
                unmanagedEntries.WriteClickUrl(entry.ClickUrl!);
            }
        }
        catch
        {
            InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);
        }
    }

    protected override async Task DownloadAssetAsyncInner(HttpClient? client, string fileUrl, Stream outputStream,
        PluginDisposableMemory<byte> fileChecksum, PluginFiles.FileReadProgressDelegate? downloadProgress,
        CancellationToken token)
    {
        await base.DownloadAssetAsyncInner(ApiDownloadHttpClient, fileUrl, outputStream, fileChecksum, downloadProgress,
            token);
    }

    private static void InitializeEmpty(out nint handle, out int count, out bool isDisposable, out bool isAllocated)
    {
        handle = nint.Zero;
        count = 0;
        isDisposable = false;
        isAllocated = false;
    }

    public override void Dispose()
    {
        if (IsDisposed) return;
        using (ThisInstanceLock.EnterScope())
        {
            ApiDownloadHttpClient?.Dispose();
            ApiResponseHttpClient?.Dispose();
            ApiResponseSocialMedia = null;
            ApiResponseNewsAndCarousel = null;
            base.Dispose();
        }
    }
}