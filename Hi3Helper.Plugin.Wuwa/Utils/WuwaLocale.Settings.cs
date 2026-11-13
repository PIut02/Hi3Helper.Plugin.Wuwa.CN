using System;
using System.Collections.Generic;

namespace Hi3Helper.Plugin.Wuwa.Utils;

/// <summary>
/// Simplified Chinese strings for the game settings page (Management/Settings/WuwaGameSettings.cs)
/// and its user-facing error messages. Keys are the exact English source strings; entries whose key
/// contains a "|" are scoped to one setting's LocalStorage key ("SettingKey|English") to
/// disambiguate labels that need different translations per setting. Strings whose Chinese form is
/// identical to English (Bloom, DLAA, 1x-16x) are intentionally absent.
/// <br/><br/>
/// Terms follow the game's own Simplified Chinese UI text (Arikatsu/WutheringWaves_Data 3.6
/// Textmaps: multi_text/MultiText.json for MenuConfig_*_Name / *_OptionsName_*, e.g. 图形质量预设,
/// 超级分辨率, 各向异性采样, 最高帧率, 护眼模式); strings the game does not expose (the ban-risk
/// warning, unverified internal settings, plugin error messages) are translated in the same tone.
/// </summary>
internal static partial class WuwaLocale
{
    internal static IReadOnlyDictionary<string, string> ChineseGameSettings { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // ---- page frame ----
            ["Wuthering Waves Game Settings"] = "鸣潮游戏设置",
            ["⚠ Ban-risk warning"] = "⚠ 封号风险提示",
            ["Editing Wuthering Waves files is unofficial and may carry a risk of account suspension or a ban. " +
             "There are no known reports of bans from these settings at this time, but safety cannot be guaranteed. " +
             "Continue only if you accept that risk. This warning is shown every time you open this page."] =
                "修改《鸣潮》游戏文件并非官方行为，可能存在账号被封停或封禁的风险。目前尚无因这些设置导致封号的报告，但无法保证绝对安全。请仅在接受该风险的前提下继续。每次打开此页面都会显示本提示。",

            // ---- display (GameUserSettings.ini) ----
            ["Display"] = "显示",
            ["Resolution width"] = "分辨率宽度",
            ["Resolution height"] = "分辨率高度",
            ["Window mode"] = "显示模式",
            ["Fullscreen"] = "全屏",
            ["Borderless"] = "无边框",
            ["Windowed"] = "窗口",
            ["VSync"] = "垂直同步",
            ["Dynamic resolution"] = "动态分辨率",
            ["HDR output"] = "HDR 输出",
            ["HDR peak brightness"] = "HDR最大亮度",
            ["Frame-rate limit"] = "最高帧率",
            ["Values not offered by the game may be unstable and can increase the account risk described above."] =
                "游戏未提供的数值可能不稳定，并可能增大上述封号风险。",

            // ---- graphics ----
            ["Graphics"] = "图像",
            ["Automatically adjust image quality"] = "自动调整画质",
            ["Graphics quality"] = "图形质量预设",
            ["Ultra Performance"] = "极致性能",
            ["ImageQuality|Performance"] = "性能优先",
            ["ImageQuality|Balanced"] = "均衡",
            ["ImageQuality|Quality"] = "画质优先",
            ["High Quality"] = "超高画质",
            ["Ultra Quality"] = "极致画质",
            ["LOD bias"] = "画面细节",
            ["Low"] = "低",
            ["Medium"] = "中",
            ["High"] = "高",
            ["Ultra High"] = "极高",
            ["Shadow quality"] = "阴影质量",
            ["Anti-aliasing"] = "抗锯齿",
            ["Off"] = "关闭",
            ["On"] = "开启",
            ["Anisotropic filtering"] = "各向异性采样",
            ["Motion blur"] = "运动模糊",
            ["Special effects quality"] = "特效质量",
            ["Crowd density"] = "人群密度",
            ["Foliage density"] = "植被密度",
            ["Render distance"] = "渲染距离",
            ["Default"] = "当前预设默认",
            ["Far"] = "远",
            ["Farthest"] = "最远",
            ["Volumetric fog"] = "体积雾",
            ["Volumetric lighting"] = "体积光",
            ["Ambient occlusion"] = "环境光遮蔽",
            ["Automatic exposure"] = "自动曝光",
            ["Character dynamic bones"] = "角色动态骨骼",
            ["Teammate effects"] = "队友特效",
            ["Vegetation dithering"] = "植被虚化",
            ["Water interaction"] = "水面交互",
            ["Flow adaptation"] = "流体自适应",
            ["Ray tracing"] = "光线追踪",
            ["NVIDIA super sampling"] = "NVIDIA 超级分辨率",
            ["NVIDIA super-sampling preset"] = "NVIDIA 超级分辨率预设",
            ["Auto"] = "自动",
            ["Ultimate Performance"] = "超级性能",
            ["Performance"] = "性能",
            ["Balanced"] = "平衡",
            ["Quality"] = "质量",
            ["NVIDIA sharpening"] = "NVIDIA 锐度",
            ["Brightness"] = "亮度",
            ["Contrast"] = "对比度",
            ["Saturation"] = "饱和度",

            // ---- audio ----
            ["Audio"] = "音频",
            ["Master volume"] = "主音量",
            ["Music volume"] = "音乐音量",
            ["Sound-effects volume"] = "音效音量",
            ["Voice volume"] = "语音音量",
            ["Ambient volume"] = "环境音量",
            ["UI volume"] = "界面音量",
            ["Play audio in the background"] = "后台播放声音",

            // ---- camera and controls ----
            ["Camera and controls"] = "相机与操作",
            ["Aim assist"] = "辅助瞄准",
            ["Auto-run"] = "自动奔跑",
            ["Auto-sprint"] = "自动疾跑",
            ["Invert horizontal camera"] = "水平视角翻转",
            ["Invert vertical camera"] = "垂直视角翻转",
            ["Sidestep camera"] = "闪避镜头",
            ["Soft-lock camera"] = "软锁定镜头",
            ["Adaptive controller triggers"] = "自适应扳机",
            ["Horizontal camera sensitivity"] = "水平视角灵敏度",
            ["Vertical camera sensitivity"] = "垂直视角灵敏度",
            ["Horizontal aim sensitivity"] = "水平瞄准灵敏度",
            ["Vertical aim sensitivity"] = "垂直瞄准灵敏度",
            ["Left-stick dead zone"] = "左摇杆死区",
            ["Right-stick dead zone"] = "右摇杆死区",
            ["Controller vibration strength"] = "控制器震动强度",
            ["Controller vibration range"] = "控制器震动范围",
            ["Full"] = "全局",
            ["Partial"] = "部分",
            ["Camera shake intensity"] = "镜头抖动强度",
            ["Regular camera distance"] = "常规镜头距离",
            ["Combat camera distance"] = "战斗镜头距离",
            ["Keyboard enemy-lock mode"] = "键盘锁定敌人模式",
            ["Controller enemy-lock mode"] = "手柄锁定敌人模式",
            ["Skill enemy-lock mode"] = "技能锁定敌人模式",
            ["The in-game mapping for this internal setting is unverified. Its current value is preserved."] =
                "此内部设置在游戏内的映射尚未验证，当前值会保持不变。",

            // ---- gameplay and accessibility ----
            ["Gameplay and accessibility"] = "玩法与辅助",
            ["Enemy hit flash"] = "怪物受击材质效果",
            ["Flight control mode"] = "飞行操作模式",
            ["Show damage numbers"] = "显示伤害飘字",
            ["Show other player names"] = "显示其他玩家名称",
            ["Subtitles"] = "字幕",
            ["Show player name in photos"] = "拍照时显示玩家名称",
            ["Walk/run transition"] = "走跑灵敏度",
            ["Eye-protection mode"] = "护眼模式",
            ["Eye-protection preset"] = "护眼模式预设",
            ["Eye-protection brightness"] = "护眼亮度",
            ["Eye-protection strength"] = "护眼强度",
            ["Eye-protection texture"] = "护眼纹理",
            ["Eye-protection color temperature"] = "护眼色温",

            // ---- language ----
            ["Language"] = "语言",
            ["Text language"] = "文字语言",
            ["Simplified Chinese"] = "简体中文",
            ["Traditional Chinese"] = "繁体中文",
            ["English"] = "英语",
            ["Japanese"] = "日语",
            ["Korean"] = "韩语",
            ["German"] = "德语",
            ["Spanish"] = "西班牙语",
            ["Portuguese"] = "葡萄牙语",
            ["French"] = "法语",
            ["Thai"] = "泰语",
            ["Voice-over language"] = "语音语言",
            ["Mandarin"] = "普通话",
            ["Voice-over languages require the corresponding voice pack to be installed."] =
                "语音语言需要安装对应的语音包。",

            // ---- dynamic strings ----
            ["Unverified value ({0})"] = "未验证的值（{0}）",

            // ---- error messages surfaced by the launcher ----
            ["Wuthering Waves is not installed or its game path is unavailable."] = "未安装《鸣潮》，或无法获取游戏路径。",
            ["Unknown Wuthering Waves setting."] = "未知的鸣潮设置项。",
            ["This setting is not available in the installed game configuration."] = "当前安装的游戏配置中不提供此设置项。",
            ["Unsupported value for {0}."] = "设置项 {0} 不支持该值。",
            ["Close Wuthering Waves before applying game settings."] = "应用游戏设置前请先关闭《鸣潮》。",
            ["Value must be between {0} and {1}."] = "数值必须在 {0} 到 {1} 之间。",
            ["Open the Wuthering Waves game settings page before changing settings."] = "请先打开鸣潮游戏设置页，再修改设置。"
        };
}
