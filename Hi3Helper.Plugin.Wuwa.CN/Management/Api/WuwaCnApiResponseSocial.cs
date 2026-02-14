using System.Text.Json.Serialization;

namespace Hi3Helper.Plugin.Wuwa.CN.Management.Api;

public class WuwaCnSocialRoot
{
    [JsonPropertyName("social")]
    public List<WuwaCnSocialEntry>? Entries { get; set; }
}

public class WuwaCnSocialEntry
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("buttonSrc")]
    public string? IconUrl { get; set; }

    [JsonPropertyName("jumpUrl")]
    public string? ClickUrl { get; set; }

    [JsonPropertyName("qrCodeSrc")]
    public string? QrCodeUrl { get; set; }

    [JsonPropertyName("qrCodeText")]
    public string? QrCodeText { get; set; }

    [JsonPropertyName("switch")]
    public int Switch { get; set; } = 1;
}