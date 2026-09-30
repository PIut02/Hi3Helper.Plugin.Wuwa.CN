﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Management.Api;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.Plugin.Core.Utility.Json;
using Microsoft.Extensions.Logging;

namespace Hi3Helper.Plugin.Wuwa.CN.Management.Api;

[GeneratedComClass]
internal partial class WuwaCnLauncherApiNews(string apiResponseBaseUrl, string gameTag, string authenticationHash) : LauncherApiNewsBase
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

    // >>> MODIFIED: Use CN-specific social model
    private WuwaCnSocialRoot? ApiResponseSocialMedia { get; set; }

    private WuwaApiResponseNews? ApiResponseNewsAndCarousel { get; set; }

    protected override async Task<int> InitAsync(CancellationToken token)
    {
        var decryptedTag = "G152";
        var decryptedAuth = "10003_Y8xXrXk65DqFHEDgApn3cpK5lfczpFx5";
        var requestNewsUrl = ApiResponseBaseUrl
            .CombineUrlFromString("launcher", decryptedAuth, decryptedTag, "information", "zh-Hans.json");

        // The official launcher builds its social media bar from this endpoint. The legacy
        // "pcstarter/prod/game/..." endpoint is still online but answers with an outdated entry set.
        var requestSocialUrl = ApiResponseBaseUrl
            .CombineUrlFromString("launcher", decryptedAuth, decryptedTag, "social", "zh-Hans.json");

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
            var json = await ApiResponseHttpClient.GetStringAsync(requestSocialUrl, token);
            ApiResponseSocialMedia = JsonSerializer.Deserialize<WuwaCnSocialRoot>(
                json, WuwaApiResponseContext.Default.WuwaCnSocialRoot);
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogWarning(
                $"[WuwaCnLauncherApiNews] Failed to load social media entries from '{requestSocialUrl}': {ex.Message}");
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
        if (entryEventCount > 0) Write(ApiResponseNewsAndCarousel.NewsData.ContentKindEvent!.Contents, ref memory, ref memIndex, LauncherNewsEntryType.Event);
        if (entryNewsCount > 0) Write(ApiResponseNewsAndCarousel.NewsData.ContentKindNews!.Contents, ref memory, ref memIndex, LauncherNewsEntryType.Info);
        if (entryNoticeCount > 0) Write(ApiResponseNewsAndCarousel.NewsData.ContentKindNotice!.Contents, ref memory, ref memIndex, LauncherNewsEntryType.Notice);

        static void Write(Span<WuwaApiResponseNewsEntry> entriesSpan, ref PluginDisposableMemory<LauncherNewsEntry> mem, ref int memOffset, LauncherNewsEntryType type)
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

        var memory = PluginDisposableMemory<LauncherCarouselEntry>.Alloc(count);
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

    // >>> MODIFIED: Rewrite GetSocialMediaEntries for CN
    public override void GetSocialMediaEntries(out nint handle, out int count, out bool isDisposable, out bool isAllocated)
    {
        if (ApiResponseSocialMedia is null || ApiResponseSocialMedia.Switch != 1)
        {
            InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);
            return;
        }

        var entries = ApiResponseSocialMedia.Entries;
        if (entries is null || entries.Count == 0)
        {
            InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);
            return;
        }

        // The host cannot render an entry without an icon and a title, so skip those.
        var validEntries = entries
            .Where(x => !string.IsNullOrEmpty(x.Name) && !string.IsNullOrEmpty(x.IconUrl))
            .ToList();

        if (validEntries.Count == 0)
        {
            InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);
            return;
        }

        var memory = PluginDisposableMemory<LauncherSocialMediaEntry>.Alloc(validEntries.Count);
        handle = memory.AsSafePointer();
        count = validEntries.Count;
        isDisposable = true;
        isAllocated = true;

        for (var i = 0; i < validEntries.Count; i++)
        {
            ref var unmanagedEntry = ref memory[i];
            var entry = validEntries[i];

            unmanagedEntry.WriteIcon(entry.IconUrl);
            unmanagedEntry.WriteDescription(entry.Name);

            // Entries such as WeChat or QQ intentionally have no jump URL and are meant to be opened
            // by scanning their QR code, so never fall back to the QR image URL as a click target.
            unmanagedEntry.WriteClickUrl(entry.IconJumpUrl);

            var qrCode = entry.QrCode;
            if (!string.IsNullOrEmpty(qrCode?.Url))
            {
                unmanagedEntry.WriteQrImage(qrCode!.Url);
                unmanagedEntry.WriteQrImageDescription(qrCode.Explain);
            }

            // Note: 'secondJumpInfoList' (e.g. the QQ groups) is intentionally not forwarded.
            // The host reads those from LauncherSocialMediaEntry.ChildEntryHandle, but
            // Hi3Helper.Plugin.Core does not expose any way for a plugin to assign that handle yet.
        }
    }
    // <<< END MODIFIED

    protected override async Task DownloadAssetAsyncInner(HttpClient? client, string fileUrl, Stream outputStream, PluginDisposableMemory<byte> fileChecksum, PluginFiles.FileReadProgressDelegate? downloadProgress, CancellationToken token)
    {
        await base.DownloadAssetAsyncInner(ApiDownloadHttpClient, fileUrl, outputStream, fileChecksum, downloadProgress, token);
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