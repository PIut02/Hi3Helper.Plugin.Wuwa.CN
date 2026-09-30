using System.Collections.Generic;
using System.Text.Json.Serialization;

// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.Wuwa.CN.Management.Api;

/// <summary>
/// Response of <c>launcher/{auth}/{gameTag}/social/{lang}.json</c>, the endpoint which the official
/// Kuro launcher uses to build its social media bar.
/// <br/>
/// <b>Note:</b> this must not be confused with the legacy <c>pcstarter/prod/game/...</c> endpoint,
/// which still answers with an outdated entry set (Kujiequ / WeChat / Weibo / QQ / Agreement /
/// Top-up) that no longer matches what the official launcher shows.
/// </summary>
public class WuwaCnSocialRoot
{
    [JsonPropertyName("switch")]
    public int Switch { get; set; } = 1;

    [JsonPropertyName("data")]
    public List<WuwaCnSocialEntry>? Entries { get; set; }
}

public class WuwaCnSocialEntry
{
    /// <summary>
    /// URL of the (monochrome) social media icon.
    /// </summary>
    [JsonPropertyName("icon")]
    public string? IconUrl { get; set; }

    /// <summary>
    /// Display name of the entry, e.g. "库街区".
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Primary click target. It is <c>null</c> for entries which are meant to be used by scanning
    /// their QR code only (for example WeChat and QQ).
    /// </summary>
    [JsonPropertyName("iconJumpUrl")]
    public string? IconJumpUrl { get; set; }

    /// <summary>
    /// Secondary click targets shown as a list. Currently not forwarded to the host, see
    /// <see cref="WuwaCnLauncherApiNews.GetSocialMediaEntries"/> for the reason.
    /// </summary>
    [JsonPropertyName("secondJumpInfoList")]
    public List<WuwaCnSocialSecondJumpInfo>? SecondJumpInfoList { get; set; }

    /// <summary>
    /// Optional QR code card shown when the entry is selected.
    /// </summary>
    [JsonPropertyName("qrCode")]
    public WuwaCnSocialQrCode? QrCode { get; set; }
}

public class WuwaCnSocialSecondJumpInfo
{
    [JsonPropertyName("tip")]
    public string? Tip { get; set; }

    [JsonPropertyName("jump")]
    public string? JumpUrl { get; set; }

    [JsonPropertyName("recordId")]
    public string? RecordId { get; set; }
}

public class WuwaCnSocialQrCode
{
    [JsonPropertyName("qrCodeUrl")]
    public string? Url { get; set; }

    [JsonPropertyName("qrCodeExplain")]
    public string? Explain { get; set; }
}
