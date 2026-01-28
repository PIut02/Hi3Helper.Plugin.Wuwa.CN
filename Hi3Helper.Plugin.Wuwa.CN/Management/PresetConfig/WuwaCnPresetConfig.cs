using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices.Marshalling;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Management.Api;
using Hi3Helper.Plugin.Wuwa.CN.Management.Api;

namespace Hi3Helper.Plugin.Wuwa.CN.Management.PresetConfig;

[GeneratedComClass]
public partial class WuwaCnPresetConfig : WuwaGlobalPresetConfig
{
    // 国服 API 地址
    private new const string ApiResponseUrl = "https://prod-cn-alicdn-gamestarter.kurogame.com/";
    private new const string ApiResponseAssetUrl = "https://pcdownload-aliyun.aki-game.com/";

    private new const string CurrentTag = "G152";
    private new const string AuthenticationHash = "10003_Y8xXrXk65DqFHEDgApn3cpK5lfczpFx5";
    
    [field: AllowNull] [field: MaybeNull] public override string ProfileName => field ??= "WuwaCn";

    [field: AllowNull] [field: MaybeNull] public override string ZoneName => field ??= "中国大陆";

    [field: AllowNull] [field: MaybeNull] public override string ZoneFullName => field ??= "鸣潮 (CN)";
    
    public override ILauncherApiMedia? LauncherApiMedia
    {
        get => field ??= new WuwaCnLauncherApiMedia(ApiResponseUrl, CurrentTag, AuthenticationHash, Hash1);
        set;
    }
    
    public override ILauncherApiNews? LauncherApiNews
    {
        get => field ??= new WuwaCnLauncherApiNews(ApiResponseUrl, CurrentTag, AuthenticationHash);
        set;
    }

    public override IGameManager? GameManager
    {
        get => field ??= new WuwaGameManager(ExecutableName, ApiResponseUrl, ApiResponseAssetUrl, AuthenticationHash,
            CurrentTag, Hash1);
        set;
    }

    public override IGameInstaller? GameInstaller
    {
        get => field ??= new WuwaGameInstaller(GameManager);
        set;
    }
}