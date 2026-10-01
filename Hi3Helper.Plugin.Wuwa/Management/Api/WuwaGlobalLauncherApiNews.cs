using Microsoft.Extensions.Logging;
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
using Hi3Helper.Plugin.Wuwa.Utils;

// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.Wuwa.Management.Api;

[GeneratedComClass]
internal partial class WuwaGlobalLauncherApiNews(string apiResponseBaseUrl, string gameTag, string authenticationHash, string apiOptions, string hash1,
    bool areParametersEncrypted = true, string localeFileName = "en.json") : LauncherApiNewsBase
{
    [field: AllowNull, MaybeNull]
    protected override HttpClient ApiResponseHttpClient
    {
        get => field ??= WuwaUtils.CreateApiHttpClient(ApiResponseBaseUrl,
            gameTag,
            authenticationHash,
            apiOptions,
            hash1,
            areParametersEncrypted,
            WuwaUtils.ApiTimeoutSeconds);
        set;
    }

    [field: AllowNull, MaybeNull]
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

    protected override string                 ApiResponseBaseUrl         { get; } = apiResponseBaseUrl;
    private            WuwaApiResponseSocial? ApiResponseSocialMedia     { get; set; }
    private            WuwaApiResponseNews?   ApiResponseNewsAndCarousel { get; set; }

    /// <summary>
    /// The launcher frees every API instance it no longer uses (<c>IFree.Free()</c> is
    /// <c>Dispose()</c>) and then asks the preset config for the interface again when the game is
    /// selected anew. It may keep using the very instance it has already freed, so an initialization
    /// has to be able to revive the instance — otherwise the second visit leaves a disposed object
    /// behind and the initialization never completes, which the launcher reports as a connection
    /// timeout.
    /// </summary>
    private void ReviveIfFreedByLauncher()
    {
        if (!IsDisposed)
        {
            return;
        }

        SharedStatic.InstanceLogger.LogWarning(
            "[WuwaGlobalLauncherApiNews] The instance was previously freed by the launcher; reviving it for re-initialization.");

        IsDisposed = false;
        ApiResponseHttpClient = null!;
        ApiDownloadHttpClient = null!;
        ApiResponseSocialMedia = null;
        ApiResponseNewsAndCarousel = null;
    }

    protected override async Task<int> InitAsync(CancellationToken token)
    {
        long startTick = Environment.TickCount64;
        SharedStatic.InstanceLogger.LogWarning("[WuwaGlobalLauncherApiNews::InitAsync] ENTER");
        ReviveIfFreedByLauncher();

        string requestSocialUrl = ApiResponseBaseUrl
               .CombineUrlFromString("launcher",
                        gameTag.ResolveApiParameter(areParametersEncrypted),
                        authenticationHash.ResolveApiParameter(areParametersEncrypted),
                        "social",
                        localeFileName);

        string requestNewsUrl = ApiResponseBaseUrl
               .CombineUrlFromString("launcher",
                        authenticationHash.ResolveApiParameter(areParametersEncrypted),
                        gameTag.ResolveApiParameter(areParametersEncrypted),
                        "information",
                        localeFileName);

        // Fetch social and news in parallel so one failing doesn't delay the other.
        long fetchTick = Environment.TickCount64;
        await Task.WhenAll(
            FetchSocialAsync(requestSocialUrl, token),
            FetchNewsAsync(requestNewsUrl, token)).ConfigureAwait(false);
        SharedStatic.InstanceLogger.LogWarning(
            "[WuwaGlobalLauncherApiNews::InitAsync] fetches done in {Elapsed} ms.", Environment.TickCount64 - fetchTick);

        // Always logged (not DEBUG gated) so that a slow initialization can be diagnosed from the
        // launcher log, where it shows up as a plugin initialization timeout.
        SharedStatic.InstanceLogger.LogWarning(
            "[WuwaGlobalLauncherApiNews::InitAsync] EXIT ok in {Elapsed} ms (social: {HasSocial}, news: {HasNews}).",
            Environment.TickCount64 - startTick, ApiResponseSocialMedia != null, ApiResponseNewsAndCarousel != null);

        return 0;
    }

    private async Task FetchSocialAsync(string url, CancellationToken token)
    {
        try
        {
            SharedStatic.InstanceLogger.LogDebug(
                "[WuwaGlobalLauncherApiNews::FetchSocialAsync] Requesting: {Url}", url);
#if !USELIGHTWEIGHTJSONPARSER
            ApiResponseSocialMedia = await ApiResponseHttpClient
                   .GetApiResponseFromJsonAsync(
                            url,
                            WuwaApiResponseContext.Default.WuwaApiResponseSocial,
                            token);
#else
            await using var socialStream = await ApiResponseHttpClient.GetStreamAsync(url, token).ConfigureAwait(false);
            ApiResponseSocialMedia = await System.Text.Json.JsonSerializer.DeserializeAsync(
                            socialStream,
                            WuwaApiResponseContext.Default.WuwaApiResponseSocial,
                            token);
#endif
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogWarning(
                "[WuwaGlobalLauncherApiNews::FetchSocialAsync] Failed to load social media entries from {Url}: {Error}",
                url, ex.Message);
        }
    }

    private async Task FetchNewsAsync(string url, CancellationToken token)
    {
        try
        {
            SharedStatic.InstanceLogger.LogDebug(
                "[WuwaGlobalLauncherApiNews::FetchNewsAsync] Requesting: {Url}", url);
#if !USELIGHTWEIGHTJSONPARSER
            ApiResponseNewsAndCarousel = await ApiResponseHttpClient
                   .GetApiResponseFromJsonAsync(
                            url,
                            WuwaApiResponseContext.Default.WuwaApiResponseNews,
                            token);
#else
            await using var newsStream = await ApiResponseHttpClient.GetStreamAsync(url, token).ConfigureAwait(false);
            ApiResponseNewsAndCarousel = await System.Text.Json.JsonSerializer.DeserializeAsync(
                            newsStream,
                            WuwaApiResponseContext.Default.WuwaApiResponseNews,
                            token);
#endif
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogWarning(
                "[WuwaGlobalLauncherApiNews::FetchNewsAsync] Failed to load news/carousel entries from {Url}: {Error}",
                url, ex.Message);
        }
    }

    public override void GetNewsEntries(out nint handle, out int count, out bool isDisposable, out bool isAllocated)
    {
        if (ApiResponseNewsAndCarousel?.NewsData == null)
        {
            InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);
            return;
        }

        int entryEventCount  = ApiResponseNewsAndCarousel.NewsData.ContentKindEvent.Contents.Length;
        int entryNewsCount   = ApiResponseNewsAndCarousel.NewsData.ContentKindNews.Contents.Length;
        int entryNoticeCount = ApiResponseNewsAndCarousel.NewsData.ContentKindNotice.Contents.Length;

        count =  entryEventCount;
        count += entryNewsCount;
        count += entryNoticeCount;

        if (count == 0)
        {
            InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);
            return;
        }

        PluginDisposableMemory<LauncherNewsEntry> memory = PluginDisposableMemory<LauncherNewsEntry>.Alloc(count);

        handle       = memory.AsSafePointer();
        isDisposable = true;
        isAllocated  = true;

        int memIndex = 0;
        Write(ApiResponseNewsAndCarousel.NewsData.ContentKindEvent.Contents,  ref memory, ref memIndex, LauncherNewsEntryType.Event);
        Write(ApiResponseNewsAndCarousel.NewsData.ContentKindNews.Contents,   ref memory, ref memIndex, LauncherNewsEntryType.Info);
        Write(ApiResponseNewsAndCarousel.NewsData.ContentKindNotice.Contents, ref memory, ref memIndex, LauncherNewsEntryType.Notice);

        return;

        static void Write(Span<WuwaApiResponseNewsEntry> entriesSpan, ref PluginDisposableMemory<LauncherNewsEntry> mem, ref int memOffset, LauncherNewsEntryType type)
        {
            for (int i = 0; i < entriesSpan.Length; i++, memOffset++)
            {
                ref LauncherNewsEntry unmanagedEntry = ref mem[memOffset];

                WuwaApiResponseNewsEntry entry = entriesSpan[i];
                unmanagedEntry.Write(entry.NewsTitle, null, entry.ClickUrl, entry.Date, type);
            }
        }
    }

    public override void GetCarouselEntries(out nint handle, out int count, out bool isDisposable, out bool isAllocated)
    {
        if (ApiResponseNewsAndCarousel == null)
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

        PluginDisposableMemory<LauncherCarouselEntry> memory = PluginDisposableMemory<LauncherCarouselEntry>.Alloc(count);

        handle       = memory.AsSafePointer();
        isDisposable = true;
        isAllocated  = true;

        Span<WuwaApiResponseCarouselEntry> entries = ApiResponseNewsAndCarousel.CarouselData;
        for (int i = 0; i < count; i++)
        {
            ref LauncherCarouselEntry unmanagedEntry = ref memory[i];

            unmanagedEntry.Write(entries[i].Description, entries[i].ImageUrl, entries[i].ClickUrl);
        }
    }

    public override void GetSocialMediaEntries(out nint handle, out int count, out bool isDisposable, out bool isAllocated)
    {
        try
        {
            if (ApiResponseSocialMedia?.SocialMediaEntries is null
             || ApiResponseSocialMedia.SocialMediaEntries.Count == 0)
            {
                SharedStatic.InstanceLogger.LogTrace(
                    "[WuwaGlobalLauncherApiNews::GetSocialMediaEntries] API provided no social media entries, returning empty handle.");
                InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);
                return;
            }

            // An entry needs a name and an icon to be renderable. It does not necessarily need a
            // click URL: some regions (e.g. the mainland China CDN) serve entries such as WeChat or
            // QQ which are meant to be opened by scanning their QR code only.
            List<WuwaApiResponseSocialResponse> validEntries =
            [
                ..ApiResponseSocialMedia.SocialMediaEntries
                                         .Where(x => !string.IsNullOrEmpty(x.SocialMediaName) &&
                                                     !string.IsNullOrEmpty(x.IconUrl) &&
                                                     (!string.IsNullOrEmpty(x.ClickUrl) ||
                                                      !string.IsNullOrEmpty(x.QrCode?.Url))
                                          )
            ];
            int entryCount = validEntries.Count;
            PluginDisposableMemory<LauncherSocialMediaEntry> memory =
                PluginDisposableMemory<LauncherSocialMediaEntry>.Alloc(entryCount);

            handle       = memory.AsSafePointer();
            count        = entryCount;
            isDisposable = true;
            isAllocated  = true;

            SharedStatic.InstanceLogger.LogTrace(
                "[WuwaGlobalLauncherApiNews::GetSocialMediaEntries] {EntryCount} entries are allocated at: 0x{Address:x8}",
                entryCount, handle);

            for (int i = 0; i < entryCount; i++)
            {
                string  socialMediaName = validEntries[i].SocialMediaName!;
                string? clickUrl        = validEntries[i].ClickUrl;
                string? iconUrl         = validEntries[i].IconUrl;
                WuwaApiResponseSocialQrCode? qrCode = validEntries[i].QrCode;

                ref LauncherSocialMediaEntry unmanagedEntries = ref memory[i];

                unmanagedEntries.WriteIcon(iconUrl);
                unmanagedEntries.WriteDescription(socialMediaName);
                unmanagedEntries.WriteClickUrl(clickUrl);

                // Forward the QR code card, otherwise the launcher has no way to display the
                // scan-to-open entries (WeChat, QQ groups, ...).
                if (!string.IsNullOrEmpty(qrCode?.Url))
                {
                    unmanagedEntries.WriteQrImage(qrCode!.Url);
                    unmanagedEntries.WriteQrImageDescription(qrCode.Explain);
                }
            }
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogError("Failed to get social media entries: {ErrorMessage}", ex.Message);
            SharedStatic.InstanceLogger.LogDebug(ex, "Exception details: {ExceptionDetails}", ex);
            InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);
        }
    }

    protected override async Task DownloadAssetAsyncInner(HttpClient? client, string fileUrl, Stream outputStream,
        PluginDisposableMemory<byte> fileChecksum, PluginFiles.FileReadProgressDelegate? downloadProgress, CancellationToken token)
    {
        await base.DownloadAssetAsyncInner(ApiDownloadHttpClient, fileUrl, outputStream, fileChecksum, downloadProgress, token).ConfigureAwait(false);
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
        if (IsDisposed)
        {
            return;
        }

        using (ThisInstanceLock.EnterScope())
        {
            // DIAGNOSTIC TRIAL: see WuwaGlobalLauncherApiMedia.Dispose — keep the instance usable after
            // the launcher frees it, because the launcher reuses the same instance on the next visit.
            SharedStatic.InstanceLogger.LogWarning("[WuwaGlobalLauncherApiNews] Free() called by the launcher; releasing caches but keeping the instance alive.");

            ApiResponseSocialMedia = null;
            ApiResponseNewsAndCarousel = null;

            ApiResponseHttpClient.Dispose();
            ApiResponseHttpClient = null!;
            ApiDownloadHttpClient.Dispose();
            ApiDownloadHttpClient = null!;
        }
    }
    
}