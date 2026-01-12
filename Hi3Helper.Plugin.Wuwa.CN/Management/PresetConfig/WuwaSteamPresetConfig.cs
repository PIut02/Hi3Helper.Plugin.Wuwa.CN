using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices.Marshalling;
using Hi3Helper.Plugin.Core.Management;

// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.Wuwa.CN.Management.PresetConfig;

[GeneratedComClass]
public partial class WuwaSteamPresetConfig : WuwaGlobalPresetConfig
{
    [field: AllowNull] [field: MaybeNull] public override string ProfileName => field ??= "WuwaSteam";

    [field: AllowNull] [field: MaybeNull] public override string ZoneName => field ??= "Steam";

    [field: AllowNull] [field: MaybeNull] public override string ZoneFullName => field ??= "Wuthering Waves (Steam)";

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