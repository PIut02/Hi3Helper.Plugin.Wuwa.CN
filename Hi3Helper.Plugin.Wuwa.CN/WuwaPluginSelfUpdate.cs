using System;
using System.Net.Http;
using System.Runtime.InteropServices.Marshalling;
using Hi3Helper.Plugin.Core.Update;
using Hi3Helper.Plugin.Core.Utility;

namespace Hi3Helper.Plugin.Wuwa.CN;

[GeneratedComClass]
// ReSharper disable once InconsistentNaming
internal partial class WuwaPluginSelfUpdate : PluginSelfUpdateBase
{
    private const string ExCdnFileSuffix = "inhouse-plugin/wuwa/";

    private const string ExCdn1Url = "https://r2.bagelnl.my.id/cl-cdn/" + ExCdnFileSuffix;
    private const string ExCdn2Url = "https://cdn.collapselauncher.com/cl-cdn/" + ExCdnFileSuffix;

    private const string ExCdn3Url =
        "https://github.com/CollapseLauncher/CollapseLauncher-ReleaseRepo/raw/main/" + ExCdnFileSuffix;

    private const string ExCdn4Url =
        "https://gitlab.com/bagusnl/CollapseLauncher-ReleaseRepo/-/raw/main/" + ExCdnFileSuffix;

    private const string ExCdn5Url = "https://cnb.cool/CollapseLauncher/ReleaseRepo/-/git/raw/main/" + ExCdnFileSuffix;

    protected readonly string[] BaseCdnUrl = [ExCdn1Url, ExCdn2Url, ExCdn3Url, ExCdn4Url, ExCdn5Url];

    internal WuwaPluginSelfUpdate()
    {
        UpdateHttpClient = new PluginHttpClientBuilder()
            .AllowRedirections()
            .AllowUntrustedCert()
            .AllowCookies()
            .Create();
    }

    protected override ReadOnlySpan<string> BaseCdnUrlSpan => BaseCdnUrl;
    protected override HttpClient UpdateHttpClient { get; }
}