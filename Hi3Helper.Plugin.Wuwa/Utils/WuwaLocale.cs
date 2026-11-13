using System;
using System.Globalization;
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
internal static partial class WuwaLocale
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

    /// <summary>
    /// Returns the Simplified Chinese form of a display string (see
    /// <see cref="ChineseGameSettings"/>), or the English source string itself for every other UI
    /// language. The English text doubles as the resource key, so strings added upstream stay
    /// readable in English instead of showing an untranslated key. Lookups run on every call —
    /// never cache the result, so a launcher language change is picked up on the next request.
    /// </summary>
    internal static string Text(string english) =>
        IsChineseUi && ChineseGameSettings.TryGetValue(english, out string? chinese) ? chinese : english;

    /// <summary>
    /// <see cref="Text(string)"/> for a format template; <paramref name="args"/> are formatted
    /// with the invariant culture.
    /// </summary>
    internal static string Text(string english, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, Text(english), args);

    /// <summary>
    /// <see cref="Text(string)"/> with a setting scope, used when the same English label needs a
    /// different translation per setting ("Quality" is 画质优先 for the graphics preset but 质量
    /// for the NVIDIA super-resolution preset). Falls back to the plain key when no
    /// <c>context|english</c> entry exists.
    /// </summary>
    internal static string TextIn(string context, string english) =>
        IsChineseUi && ChineseGameSettings.TryGetValue(string.Concat(context, "|", english), out string? contextual)
            ? contextual
            : Text(english);
}
