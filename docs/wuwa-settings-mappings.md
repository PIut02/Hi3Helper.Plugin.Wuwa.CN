# Wuthering Waves settings mappings

Option values and English labels come from extracted **3.6** game tables, matching the inspected installation's version 3.6.0. These are community-hosted game-data extracts, not official API documentation.

Sources:
- [menuconfig.json](https://github.com/Arikatsu/WutheringWaves_Data/blob/3.6/BinData/menu/menuconfig.json): pair OptionsValue with localized OptionsName; use SliderRange for slider bounds.
- [English MultiText.json](https://github.com/Arikatsu/WutheringWaves_Data/blob/3.6/Textmaps/en/multi_text/MultiText.json): option text.
- [languagedefine.json](https://github.com/Arikatsu/WutheringWaves_Data/blob/3.6/BinData/menu/languagedefine.json): LanguageType IDs; only IsShow languages are offered.
- [rolevoicelanguage.json](https://github.com/Arikatsu/WutheringWaves_Data/blob/3.6/BinData/menu/rolevoicelanguage.json): voice language IDs and text.

| LocalStorage key | Menu FunctionId |
| --- | --- |
| ImageQuality | 10 |
| ShadowQuality | 54 |
| NiagaraQuality | 55 |
| ImageDetail | 56 |
| AntiAliasing | 57 |
| VolumeFog | 63 |
| VolumeLight | 64 |
| MotionBlur | 65 |
| NpcDensity | 79 |
| NvidiaSuperSamplingQuality | 831 |
| CameraShakeStrength | 93 |
| CommonSpringArmLength | 99 |
| FightSpringArmLength | 100 |
| JoystickShakeType | 105 |
| EnemyHitDisplayMode | 135 |
| AnisoLevel | 20004 |
| RayTracing | 20026 |
| VegetationDensity | 20035 |
| LoadingRangeScaleLevel | 20611 |

The LocalStorage-key associations are inferred from matching names and semantics; the menu table itself uses FunctionIds. A read-only database check corroborated ImageDetail=2, ShadowQuality=3, VolumeLight=1, ImageQuality=3 and NVIDIA quality=99. This is not an exhaustive in-game round-trip test.

Graphics presets merge device-dependent lists. Availability still depends on hardware and game version; the plugin does not evaluate the game's Device predicates. Existing values outside the lists remain visible as `Unverified value (n)` and are not changed merely by opening or applying the page.

Unresolved internal enums: KeyboardLockEnemyMode, GamepadLockEnemyMode, SkillLockEnemyMode, FlyControlMode and EyeProtectionMode. These offer only their existing value until their storage mapping is established. Do not infer their values from similarly named menu controls.

The generic Level lists are removed. Ray tracing becomes a quality selector, and regular/combat camera distance becomes a 0–100 slider. No installed game files are modified by this source change.

## Localization

Display strings (section/entry titles, descriptions, choice labels, the ban-risk warning and the
user-facing error messages) are localized through `Utils/WuwaLocale.Text*` and the Simplified
Chinese table in `Utils/WuwaLocale.Settings.cs`. The English source text doubles as the resource
key, so strings added upstream stay readable in English instead of showing an untranslated key;
labels that need a different translation per setting use a `SettingKey|English` key (for example
`ImageQuality|Quality` = 画质优先 vs. `NvidiaSuperSamplingQuality`'s 质量).

Chinese terms follow the game's own Simplified Chinese UI text (Arikatsu/WutheringWaves_Data **3.6**
Textmaps: `zh-Hans/multi_text/MultiText.json` for the `MenuConfig_*_Name` / `*_OptionsName_*` keys
pointed at by the FunctionId table above, e.g. 图形质量预设, 超级分辨率, 各向异性采样, 最高帧率,
护眼模式); settings the game does not expose (ban-risk warning, unverified internal settings,
plugin errors) are translated in the same tone.

`tests/GameSettingsLocale` fails the build when a page string has no Chinese entry, when a table
entry becomes unreachable (dead key), or when the locale switch stops re-rendering the page.
