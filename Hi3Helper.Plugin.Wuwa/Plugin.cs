using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management.PresetConfig;
using Hi3Helper.Plugin.Core.Update;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.Plugin.Wuwa.Management.PresetConfig;
using Hi3Helper.Plugin.Wuwa.Utils;
using System;
using System.Runtime.InteropServices.Marshalling;

// ReSharper disable InconsistentNaming
namespace Hi3Helper.Plugin.Wuwa;

[GeneratedComClass]
public partial class WuwaPlugin : PluginBase
{
    // Index 0 is the region shown first, so the mainland China region leads for this fork.
    private static readonly IPluginPresetConfig[] PresetConfigInstances = [new WuwaCnPresetConfig(), new WuwaGlobalPresetConfig(), new WuwaSteamPresetConfig(), new WuwaEpicPresetConfig()];
    private static DateTime _pluginCreationDate = new(2025, 07, 20, 05, 06, 0, DateTimeKind.Utc);

    public override void GetPluginName(out string result) => result = "Wuthering Waves Plugin";

    public override void GetPluginDescription(out string result) => result = "A plugin for Wuthering Waves in Collapse Launcher";

    public override void GetPluginAuthor(out string result) => result = "CryoTechnic, Collapse Project Team";

    public override unsafe void GetPluginCreationDate(out DateTime* result) => result = _pluginCreationDate.AsPointer();

    public override void GetPresetConfigCount(out int count) => count = PresetConfigInstances.Length;

    public override void GetPresetConfig(int index, out IPluginPresetConfig presetConfig)
    {
        // Avoid crash by returning null if index is out of bounds
        if (index < 0 || index >= PresetConfigInstances.Length)
        {
            presetConfig = null!;
            return;
        }

        // Return preset config at index (n)
        presetConfig = PresetConfigInstances[index];
    }

    /// <summary>
    /// Self-update is intentionally left unwired for this build.
    /// <br/>
    /// The self-updater published by the upstream project points to Collapse's in-house plugin CDN
    /// (<c>cl-cdn/inhouse-plugin/wuwa/</c>), which only hosts the official build. Running it would
    /// replace this build and silently drop the mainland China region, so <c>null</c> is returned
    /// and the launcher skips the check (<c>WuwaPluginSelfUpdate</c> is kept only for reference).
    /// </summary>
    public override void GetPluginSelfUpdater(out IPluginSelfUpdate? selfUpdate) => selfUpdate = null;

    private string? _getPluginAppIconUrl;

    public override void GetPluginAppIconUrl(out string result) => result = _getPluginAppIconUrl ??= Convert.ToBase64String(WuwaImageData.WuwaAppIconData);

    private string? _getNotificationPosterUrl;

	public override void GetNotificationPosterUrl(out string result) => result = _getNotificationPosterUrl ??= Convert.ToBase64String(WuwaImageData.WuwaPosterData);
}
