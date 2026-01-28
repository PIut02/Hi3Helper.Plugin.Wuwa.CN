using System;
using System.Runtime.InteropServices.Marshalling;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management.PresetConfig;
using Hi3Helper.Plugin.Core.Update;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.Plugin.Wuwa.CN.Management.PresetConfig;
using Hi3Helper.Plugin.Wuwa.CN.Utils;

// ReSharper disable InconsistentNaming
namespace Hi3Helper.Plugin.Wuwa.CN;

[GeneratedComClass]
public partial class WuwaPlugin : PluginBase
{
    private static readonly IPluginPresetConfig[] PresetConfigInstances =
    [
        new WuwaCnPresetConfig(),
    ];

    private static DateTime _pluginCreationDate = new(2025, 07, 20, 05, 06, 0, DateTimeKind.Utc);

    private string? _getNotificationPosterUrl;

    private string? _getPluginAppIconUrl;

    public override void GetPluginName(out string result)
    {
        result = "鸣潮插件";
    }

    public override void GetPluginDescription(out string result)
    {
        result = "基于鸣潮国际服插件的国服化插件";
    }

    public override void GetPluginAuthor(out string result)
    {
        result = "CryoTechnic, Collapse Project Team, Misaka10843";
    }

    public override unsafe void GetPluginCreationDate(out DateTime* result)
    {
        result = _pluginCreationDate.AsPointer();
    }

    public override void GetPresetConfigCount(out int count)
    {
        count = PresetConfigInstances.Length;
    }

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

    public override void GetPluginSelfUpdater(out IPluginSelfUpdate selfUpdate)
    {
        selfUpdate = null!;
    }

    public override void GetPluginAppIconUrl(out string result)
    {
        result = _getPluginAppIconUrl ??= Convert.ToBase64String(WuwaImageData.WuwaAppIconData);
    }

    public override void GetNotificationPosterUrl(out string result)
    {
        result = _getNotificationPosterUrl ??= Convert.ToBase64String(WuwaImageData.WuwaPosterData);
    }
}