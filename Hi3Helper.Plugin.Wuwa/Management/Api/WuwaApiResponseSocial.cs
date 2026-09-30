using System.Collections.Generic;
using System.Text.Json.Serialization;
// ReSharper disable IdentifierTypo

// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.Wuwa.Management.Api;

public class WuwaApiResponseSocial
{
    [JsonPropertyName("data")] // Mapping: root -> data[]
    public List<WuwaApiResponseSocialResponse>? SocialMediaEntries { get; set; }
}

public class WuwaApiResponseSocialResponse
{
    [JsonPropertyName("iconJumpUrl")] // Mapping: root -> data[] -> iconJumpUrl
    public string? ClickUrl { get; set; }
    
    [JsonPropertyName("icon")] // Mapping: root -> data[] -> icon
    public string? IconUrl { get; set; }
    
    [JsonPropertyName("name")] // Mapping: root -> data[] -> name
    public string? SocialMediaName { get; set; }

    /// <summary>
    /// Optional QR code card shown for entries which are meant to be opened by scanning,
    /// instead of by clicking a link (e.g. WeChat, QQ groups).
    /// </summary>
    [JsonPropertyName("qrCode")]
    public WuwaApiResponseSocialQrCode? QrCode { get; set; }

    /// <summary>
    /// Optional list of secondary links belonging to this entry.
    /// <br/>
    /// <b>Note:</b> these are currently not forwarded to the launcher. The host reads them from
    /// <c>LauncherSocialMediaEntry.ChildEntryHandle</c>, but <c>Hi3Helper.Plugin.Core</c> does not
    /// expose a way for a plugin to assign that handle yet.
    /// </summary>
    [JsonPropertyName("secondJumpInfoList")]
    public List<WuwaApiResponseSocialSecondJumpInfo>? SecondJumpInfoList { get; set; }
}

public class WuwaApiResponseSocialSecondJumpInfo
{
    [JsonPropertyName("tip")] // Mapping: root -> data[] -> secondJumpInfoList[] -> tip
    public string? Tip { get; set; }

    [JsonPropertyName("jump")] // Mapping: root -> data[] -> secondJumpInfoList[] -> jump
    public string? JumpUrl { get; set; }

    [JsonPropertyName("recordId")] // Mapping: root -> data[] -> secondJumpInfoList[] -> recordId
    public string? RecordId { get; set; }
}

public class WuwaApiResponseSocialQrCode
{
    [JsonPropertyName("qrCodeUrl")] // Mapping: root -> data[] -> qrCode -> qrCodeUrl
    public string? Url { get; set; }

    [JsonPropertyName("qrCodeExplain")] // Mapping: root -> data[] -> qrCode -> qrCodeExplain
    public string? Explain { get; set; }
}
