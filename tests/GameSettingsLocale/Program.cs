using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.UI.Settings;
using Hi3Helper.Plugin.Wuwa.Management.Api;

// Verifies the game settings localization end to end:
//   1. every display string on the page has a Simplified Chinese entry (no untranslated UI),
//   2. every table entry is reachable (no dead i18n keys),
//   3. structure (keys, values, ranges, choice values) is identical in both languages,
//   4. switching the launcher locale re-renders the page (nothing is cached),
//   5. user-facing error messages are localized too.
var assembly = typeof(WuwaApiResponseContext).Assembly;
const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
int passed = 0;
void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }

Type settingsType = assembly.GetType("Hi3Helper.Plugin.Wuwa.Management.Settings.WuwaGameSettings", true)!;
Type localeType = assembly.GetType("Hi3Helper.Plugin.Wuwa.Utils.WuwaLocale", true)!;
var table = (IReadOnlyDictionary<string, string>)localeType.GetProperty("ChineseGameSettings", all)!.GetValue(null)!;
PropertyInfo localeProperty = typeof(SharedStatic).GetProperty(nameof(SharedStatic.PluginLocaleCode))!;
void SetLocale(string code) => localeProperty.GetSetMethod(true)!.Invoke(null, [code]);

// Fixture: a GameUserSettings.ini plus a LocalStorage value for every known key, so the page
// renders every section and entry without an installed game.
string scratch = Path.Combine(Path.GetTempPath(), "wuwa-settings-locale-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(scratch);
string iniPath = Path.Combine(scratch, "GameUserSettings.ini");
File.WriteAllText(iniPath,
    "[/Script/Engine.GameUserSettings]\n" +
    "ResolutionSizeX=1920\nResolutionSizeY=1080\nFullscreenMode=1\nbUseVSync=True\n" +
    "bUseDynamicResolution=False\nbUseHDRDisplayOutput=True\nHDRDisplayOutputNits=1000\nFrameRateLimit=120\n");
object ini = assembly.GetType("Hi3Helper.Plugin.Wuwa.Management.Settings.WuwaIniDocument", true)!
    .GetMethod("Load", all)!.Invoke(null, [iniPath])!;

Dictionary<string, string> localValues = new(StringComparer.Ordinal);
foreach (object section in (System.Collections.IEnumerable)settingsType
             .GetField("LocalSettingSections", all)!.GetValue(null)!)
{
    foreach (object setting in (System.Collections.IEnumerable)section.GetType().GetProperty("Settings")!.GetValue(section)!)
    {
        Type type = setting.GetType();
        string key = (string)type.GetProperty("Key")!.GetValue(setting)!;
        string kind = type.GetProperty("Kind")!.GetValue(setting)!.ToString()!;
        if (kind != "Choice")
        {
            double minimum = Convert.ToDouble(type.GetProperty("Minimum")!.GetValue(setting), CultureInfo.InvariantCulture);
            localValues[key] = minimum.ToString(CultureInfo.InvariantCulture);
            continue;
        }

        object? first = ((System.Collections.IEnumerable)type.GetProperty("Choices")!.GetValue(setting)!)
            .Cast<object>().FirstOrDefault();
        localValues[key] = first == null
            ? "0" // empty choice list -> exercises the "Unverified value" fallback
            : (string)first.GetType().GetProperty("Value")!.GetValue(first)!;
    }
}

object CreateSettings() => Activator.CreateInstance(settingsType, all, null, [scratch, null, ini, localValues], null)!;
GameSettingsPage CreatePage(string locale)
{
    SetLocale(locale);
    return (GameSettingsPage)settingsType.GetMethod("CreatePage", all)!.Invoke(CreateSettings(), null)!;
}

void Walk(GameSettingsPage page, List<string> strings, Dictionary<string, List<string>> choicesBySetting)
{
    if (page.Title != null) strings.Add(page.Title);
    foreach (GameSettingsSection section in page.Sections)
    {
        if (section.Title != null) strings.Add(section.Title);
        if (section.Description != null) strings.Add(section.Description);
        foreach (GameSettingEntry entry in section.Entries)
        {
            strings.Add(entry.Title);
            if (entry.Description != null) strings.Add(entry.Description);
            if (entry.Choices == null) continue;
            string settingKey = entry.Key.StartsWith("local.", StringComparison.Ordinal) ? entry.Key["local.".Length..] : entry.Key;
            if (!choicesBySetting.TryGetValue(settingKey, out List<string>? titles))
            {
                titles = [];
                choicesBySetting[settingKey] = titles;
            }
            foreach (GameSettingChoice choice in entry.Choices)
            {
                strings.Add(choice.Title);
                titles.Add(choice.Title);
            }
        }
    }
}

const string UnverifiedTemplate = "Unverified value ({0})";
Regex unverified = new(@"^Unverified value \(\d+\)$", RegexOptions.Compiled);

// English strings that stay identical in Simplified Chinese (technical marks).
HashSet<string> identityStrings = ["Bloom", "DLAA", "1x", "2x", "4x", "8x", "16x"];
// Error-message keys that never appear on the page itself.
HashSet<string> errorKeys =
[
    "Wuthering Waves is not installed or its game path is unavailable.",
    "Unknown Wuthering Waves setting.",
    "This setting is not available in the installed game configuration.",
    "Unsupported value for {0}.",
    "Close Wuthering Waves before applying game settings.",
    "Value must be between {0} and {1}.",
    "Open the Wuthering Waves game settings page before changing settings."
];

List<string> english = [];
Dictionary<string, List<string>> englishChoices = new(StringComparer.Ordinal);
GameSettingsPage enPage = CreatePage("en-us");
Walk(enPage, english, englishChoices);

void Test(string name, Action action)
{
    try
    {
        action();
        passed++;
        Console.WriteLine($"PASS  {name}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAIL  {name}: {ex.Message}");
        Environment.Exit(1);
    }
}

Test("page renders all sections", () =>
{
    Assert(enPage.Title == "Wuthering Waves Game Settings", $"unexpected page title {enPage.Title}");
    Assert(enPage.Sections.Count == 7, $"expected 7 sections, got {enPage.Sections.Count}"); // ban + display + 5 local
});

Test("every page string has a Chinese entry", () =>
{
    List<string> missing = [];
    foreach (string s in english)
    {
        if (identityStrings.Contains(s)) continue;
        if (unverified.IsMatch(s)) continue; // covered by the template key below
        if (!table.ContainsKey(s)) missing.Add(s);
    }
    Assert(table.ContainsKey(UnverifiedTemplate), "missing zh for the unverified-value template");
    Assert(missing.Count == 0, "missing zh entries: " + string.Join(" | ", missing));
});

Test("no dead i18n keys", () =>
{
    HashSet<string> used = new(StringComparer.Ordinal);
    foreach (string s in english)
    {
        if (unverified.IsMatch(s)) { used.Add(UnverifiedTemplate); continue; }
        used.Add(s);
    }
    foreach ((string settingKey, List<string> titles) in englishChoices)
        foreach (string title in titles)
            used.Add(table.ContainsKey(settingKey + "|" + title) ? settingKey + "|" + title : title);
    used.UnionWith(errorKeys);

    List<string> dead = table.Keys.Where(key => !used.Contains(key)).ToList();
    Assert(dead.Count == 0, "unreachable table entries: " + string.Join(" | ", dead));
});

GameSettingsPage zhPage = CreatePage("zh-cn");
List<string> chinese = [];
Dictionary<string, List<string>> chineseChoices = new(StringComparer.Ordinal);
Walk(zhPage, chinese, chineseChoices);

Test("structure is identical across locales", () =>
{
    Assert(zhPage.Sections.Count == enPage.Sections.Count, "section count changed");
    for (int i = 0; i < enPage.Sections.Count; i++)
    {
        GameSettingsSection enSection = enPage.Sections[i];
        GameSettingsSection zhSection = zhPage.Sections[i];
        Assert(enSection.Entries.Count == zhSection.Entries.Count, $"entry count changed in section {i}");
        for (int j = 0; j < enSection.Entries.Count; j++)
        {
            GameSettingEntry enEntry = enSection.Entries[j];
            GameSettingEntry zhEntry = zhSection.Entries[j];
            Assert(enEntry.Key == zhEntry.Key, $"key changed: {enEntry.Key} -> {zhEntry.Key}");
            Assert(enEntry.Value == zhEntry.Value, $"value changed for {enEntry.Key}");
            Assert(enEntry.Kind == zhEntry.Kind, $"kind changed for {enEntry.Key}");
            Assert(enEntry.Minimum == zhEntry.Minimum && enEntry.Maximum == zhEntry.Maximum && enEntry.Step == zhEntry.Step,
                $"range changed for {enEntry.Key}");
            if (enEntry.Choices == null) { Assert(zhEntry.Choices == null, $"choices appeared for {enEntry.Key}"); continue; }
            Assert(zhEntry.Choices != null && enEntry.Choices.Count == zhEntry.Choices.Count, $"choice count changed for {enEntry.Key}");
            for (int k = 0; k < enEntry.Choices.Count; k++)
                Assert(enEntry.Choices[k].Value == zhEntry.Choices![k].Value, $"choice value changed for {enEntry.Key}");
        }
    }
});

Test("Chinese page is fully translated", () =>
{
    Assert(zhPage.Title == "鸣潮游戏设置", $"unexpected zh page title {zhPage.Title}");
    for (int i = 0; i < enPage.Sections.Count; i++)
    {
        Assert(zhPage.Sections[i].Title != enPage.Sections[i].Title, $"section {i} title not translated");
        GameSettingsSection enSection = enPage.Sections[i];
        GameSettingsSection zhSection = zhPage.Sections[i];
        Assert((enSection.Description == null) == (zhSection.Description == null), $"section {i} description presence changed");
        if (enSection.Description != null) Assert(zhSection.Description != enSection.Description, $"section {i} description not translated");
        for (int j = 0; j < enSection.Entries.Count; j++)
        {
            GameSettingEntry enEntry = enSection.Entries[j];
            GameSettingEntry zhEntry = zhSection.Entries[j];
            if (!identityStrings.Contains(enEntry.Title))
                Assert(zhEntry.Title != enEntry.Title, $"{enEntry.Key} title not translated");
            Assert((enEntry.Description == null) == (zhEntry.Description == null), $"{enEntry.Key} description presence changed");
            if (enEntry.Description != null) Assert(zhEntry.Description != enEntry.Description, $"{enEntry.Key} description not translated");
            if (enEntry.Choices == null) continue;
            for (int k = 0; k < enEntry.Choices.Count; k++)
                if (!identityStrings.Contains(enEntry.Choices[k].Title))
                    Assert(zhEntry.Choices![k].Title != enEntry.Choices[k].Title, $"{enEntry.Key} choice '{enEntry.Choices[k].Title}' not translated");
        }
    }
});

Test("unverified choice fallback is localized", () =>
{
    GameSettingsSection? camera = zhPage.Sections.FirstOrDefault(s => s.Title == "相机与操作");
    Assert(camera != null, "camera section missing");
    GameSettingEntry? lockMode = camera!.Entries.FirstOrDefault(e => e.Key == "local.KeyboardLockEnemyMode");
    Assert(lockMode?.Choices is { Count: 1 }, "expected a single unverified choice");
    Assert(lockMode!.Choices![0].Title == "未验证的值（0）", $"unexpected zh fallback '{lockMode.Choices[0].Title}'");
});

Test("context-scoped labels resolve per setting", () =>
{
    GameSettingEntry? quality = zhPage.Sections.SelectMany(s => s.Entries).FirstOrDefault(e => e.Key == "local.ImageQuality");
    GameSettingEntry? dlss = zhPage.Sections.SelectMany(s => s.Entries).FirstOrDefault(e => e.Key == "local.NvidiaSuperSamplingQuality");
    Assert(quality?.Choices?.First(c => c.Value == "1").Title == "性能优先", "graphics preset not context-translated");
    Assert(quality?.Choices?.First(c => c.Value == "3").Title == "画质优先", "graphics preset not context-translated");
    Assert(dlss?.Choices?.First(c => c.Value == "1").Title == "质量", "DLSS preset not context-translated");
});

Test("locale switches re-render the page", () =>
{
    Assert(CreatePage("en-us").Title == "Wuthering Waves Game Settings", "en title not restored (cached zh string?)");
    Assert(CreatePage("zh-cn").Title == "鸣潮游戏设置", "zh title not applied on re-render");
});

object zhInstance = CreateSettings();
Exception Error(Action action)
{
    try { action(); throw new Exception("expected an exception"); }
    catch (Exception ex) { return ex is TargetInvocationException invocation ? invocation.InnerException! : ex; }
}

Test("error messages are localized", () =>
{
    SetLocale("zh-cn");
    string zhRange = Error(() => settingsType.GetMethod("SetValue", all)!.Invoke(zhInstance, ["display.frameRate", "10"])).Message;
    Assert(zhRange.StartsWith("数值必须在 30 到 360 之间。"), $"zh range message mismatch: {zhRange.ReplaceLineEndings(" | ")}");
    string zhUnknown = Error(() => settingsType.GetMethod("SetValue", all)!.Invoke(zhInstance, ["not-a-setting", "1"])).Message;
    Assert(zhUnknown.StartsWith("未知的鸣潮设置项。"), $"zh unknown-key message mismatch: {zhUnknown.ReplaceLineEndings(" | ")}");

    SetLocale("en-us");
    string enRange = Error(() => settingsType.GetMethod("SetValue", all)!.Invoke(zhInstance, ["display.frameRate", "10"])).Message;
    Assert(enRange.StartsWith("Value must be between 30 and 360."), $"en range message mismatch: {enRange.ReplaceLineEndings(" | ")}");
});

SetLocale("en-us");
Console.WriteLine($"\n{passed} localization checks passed.");
