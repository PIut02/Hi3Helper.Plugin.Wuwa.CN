using Hi3Helper.Plugin.Core.Management;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices.Marshalling;

// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

namespace Hi3Helper.Plugin.Wuwa.Management.PresetConfig;

/// <summary>
/// Mainland China region ("国服") of Wuthering Waves.
/// <br/><br/>
/// It shares the whole implementation with <see cref="WuwaGlobalPresetConfig"/> and only supplies
/// the region specific endpoint/credential values. Note that the mainland CDN hands out the game
/// tag and the authentication hash verbatim, whereas the global CDN obfuscates them, hence
/// <see cref="AreApiParametersEncrypted"/> is disabled here.
/// </summary>
[GeneratedComClass]
public partial class WuwaCnPresetConfig : WuwaGlobalPresetConfig
{
    // ---------------------------------------------------------------- region endpoints
    protected override string ApiResponseUrl => "https://prod-cn-alicdn-gamestarter.kurogame.com/";
    protected override string ApiResponseAssetUrl => "https://pcdownload-aliyun.aki-game.com/";
    protected override string CurrentTag => "G152";
    protected override string AuthenticationHash => "10003_Y8xXrXk65DqFHEDgApn3cpK5lfczpFx5";

    /// <summary>
    /// The mainland CDN returns these values in plain text, so they must not be de-obfuscated.
    /// </summary>
    protected override bool AreApiParametersEncrypted => false;

    protected override string ApiLocaleFileName => "zh-Hans.json";

    // ---------------------------------------------------------------- installation detection
    protected override string ThirdPartySdkFolderName => "KrPcSdk_Mainland";
    protected override string SdkMarkerFileName => "KRSDKConfig.json";

    // ---------------------------------------------------------------- region metadata
    // NOTE: 'GameName' is deliberately NOT overridden. The launcher groups all regions into a single
    // game entry by that value, so overriding it here would make the mainland region show up as a
    // second game next to the global one.
    [field: AllowNull, MaybeNull]
    public override string ProfileName => field ??= "WuwaCn";

    /// <summary>
    /// Region label. The canonical (English) name is used on purpose: the launcher translates it
    /// through its own <c>_GameClientRegions</c> table, which maps it to 中国大陆 for Chinese UIs.
    /// </summary>
    [field: AllowNull, MaybeNull]
    public override string ZoneName => field ??= "Mainland China";

    [field: AllowNull, MaybeNull]
    public override string ZoneFullName => field ??= "鸣潮 (中国大陆)";

    [field: AllowNull, MaybeNull]
    public override string GameMainLanguage => field ??= "zh-Hans";

    public override GameReleaseChannel ReleaseChannel => GameReleaseChannel.Public;

    [field: AllowNull, MaybeNull]
    public override string ZoneHomePageUrl => field ??= "https://mc.kurogames.com/";

    [field: AllowNull, MaybeNull]
    public override string ZoneDescription => field ??=
        "《鸣潮》是一款强调开放世界动作类游戏，拥有高自由度的战斗玩法和丰富多样的叙事内容。玩家将扮演沉睡后苏醒的漂泊者，与众多共鸣者一同踏上找回遗失记忆的旅途。";

    public override IGameManager? GameManager
    {
        get => field ??= new WuwaGameManager(ExecutableName, ApiResponseUrl, ApiResponseAssetUrl, AuthenticationHash,
            CurrentTag, Hash1, AreApiParametersEncrypted, ThirdPartySdkFolderName, SdkMarkerFileName,
            LauncherGameDirectoryName, SupportsPatchAndPreload);
        set;
    }

    /// <summary>
    /// The upstream update/patch/preload flow still resolves its hotfix/patch sources from
    /// global-only CDN endpoints (see <c>WuwaKnownHotfixPatch.WindowsCdnRoot</c>), and the mainland
    /// download/patch endpoints have not been validated end-to-end yet. Until that is done, the
    /// flow stays disabled for this region so an unverified patch run can never touch an existing
    /// game installation. Set <c>WUWA_FORCE_ENABLE_PATCHING=1</c> to try it out without rebuilding.
    /// </summary>
    protected override bool SupportsPatchAndPreload => false;

    public override IGameInstaller? GameInstaller
    {
        get => field ??= new WuwaGameInstaller(GameManager);
        set;
    }
}
