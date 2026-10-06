using System.Collections.Generic;
using System.Text.Json.Serialization;
using Hi3Helper.Plugin.Core.Management;

namespace Hi3Helper.Plugin.Wuwa.Management.Api;

/// <summary>
/// Represents Kuro's launcher download config file (<c>launcherDownloadConfig.json</c>),
/// which the official launcher uses to track the installed game version.
/// <br/><br/>
/// Note that its top level <see cref="Version"/> field can lag behind the resource bundles listed in
/// <see cref="Bundles"/> — use <c>WuwaKuroVersionCrossCheck.TryResolve()</c> to obtain the version which
/// is actually installed instead of reading <see cref="Version"/> directly.
/// </summary>
public class WuwaLauncherDownloadConfig
{
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("reUseVersion")]
    public string? ReUseVersion { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("isPreDownload")]
    public bool IsPreDownload { get; set; }

    [JsonPropertyName("appId")]
    public string? AppId { get; set; }

    /// <summary>
    /// Per resource pack state, keyed by pack name (e.g. <c>HD</c>). Kuro bumps the version of these
    /// entries on every content update, which makes them the reliable half of this file.
    /// </summary>
    [JsonPropertyName("bundles")]
    public Dictionary<string, WuwaLauncherDownloadConfigBundle>? Bundles { get; set; }
}

/// <summary>
/// A single resource pack entry inside <c>launcherDownloadConfig.json</c>'s <c>bundles</c> map.
/// </summary>
public class WuwaLauncherDownloadConfigBundle
{
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("resourcePacks")]
    public string[]? ResourcePacks { get; set; }
}
