// Endpoint discovery and core implementation idea credit: DynamiByte
// References:
//   https://gist.github.com/DynamiByte/d839bf9f671c975b6666d0f6e6634641
//   https://github.com/Cheu3172/Wuwa-Web-Request
using System.Text.Json.Serialization;
// ReSharper disable IdentifierTypo

namespace Hi3Helper.Plugin.Wuwa.Management.Api;

public class WuwaApiResponseLauncherConfig
{
    /// <summary>
    /// New (global) game-config shape: <c>root -> config -> functionCode -> background</c>.
    /// </summary>
    [JsonPropertyName("config")]
    public WuwaApiResponseLauncherConfigSection? Config { get; set; }

    /// <summary>
    /// Legacy launcher-config shape still served by the mainland China CDN:
    /// <c>root -> functionCode -> background</c>. Kept as a fallback so the same media
    /// implementation can serve both CDN generations.
    /// </summary>
    [JsonPropertyName("functionCode")] // Mapping: root -> functionCode
    public WuwaApiResponseLauncherConfigFunctionCode? FunctionCode { get; set; }
}

public class WuwaApiResponseLauncherConfigSection
{
    [JsonPropertyName("functionCode")] // Mapping: root -> config -> functionCode
    public WuwaApiResponseLauncherConfigFunctionCode? FunctionCode { get; set; }
}

public class WuwaApiResponseLauncherConfigFunctionCode
{
    /// <summary>
    /// The current background hash that rotates with each launcher background update.
    /// Used as the <c>&lt;BACKGROUND_HASH&gt;</c> path segment in the wallpapers-slogan URL:
    /// <c>.../background/{Background}/en.json</c>
    /// </summary>
    [JsonPropertyName("background")] // Mapping: root -> config -> functionCode -> background
    public string? Background { get; set; }
}
