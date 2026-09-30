using System;
using Hi3Helper.Plugin.Core;

// ReSharper disable IdentifierTypo

namespace Hi3Helper.Plugin.Wuwa.Utils;

/// <summary>
/// Provides display strings in the language the launcher currently runs in.
/// <br/><br/>
/// The launcher pushes its UI language to the plugin <b>before</b> it asks for the preset configs
/// (<c>CollapseLauncher/Classes/Plugins/PluginInfo.cs</c> calls
/// <c>pluginInstance.SetPluginLocaleId(LauncherConfig.GetAppConfigValue("AppLanguage"))</c> right
/// after <c>GetPresetConfigCount()</c> and before <c>GetPresetConfig(i, ...)</c>), and it reads the
/// preset's <c>GameName</c> only later, when the game list is assembled
/// (<c>PluginManager.LoadPlugins</c>). The locale is therefore already known by the time any preset
/// property is read.
/// <br/><br/>
/// The launcher also runs every value returned from a preset through its own
/// <c>_GameClientTitles</c> / <c>_GameClientRegions</c> tables, but those tables currently have no
/// entry for "Wuthering Waves", so the plugin has to hand out the matching language itself.
/// </summary>
internal static class WuwaLocale
{
    /// <summary>
    /// Whether the launcher currently runs with a Chinese UI language.
    /// </summary>
    internal static bool IsChineseUi =>
        SharedStatic.PluginLocaleCode.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns <paramref name="chineseValue"/> for a Chinese UI and <paramref name="englishValue"/>
    /// otherwise.
    /// </summary>
    internal static string Get(string englishValue, string chineseValue) =>
        IsChineseUi ? chineseValue : englishValue;
}
