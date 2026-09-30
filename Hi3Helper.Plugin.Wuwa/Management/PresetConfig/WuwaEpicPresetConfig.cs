using Hi3Helper.Plugin.Core.Management;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices.Marshalling;

// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming

namespace Hi3Helper.Plugin.Wuwa.Management.PresetConfig;

[GeneratedComClass]
public partial class WuwaEpicPresetConfig : WuwaGlobalPresetConfig
{
	private new const string AuthenticationHash = "VlNTU1A8EBAlFiBUFQQTBQAPUy0aNikWBi4CAlYtCw8yBwIEBzY";

	[field: AllowNull, MaybeNull]
	public override string ProfileName => field ??= "WuwaEpic";

	[field: AllowNull, MaybeNull]
	public override string ZoneName => field ??= "Epic Games";

	[field: AllowNull, MaybeNull]
	public override string ZoneFullName => field ??= "Wuthering Waves (Epic Games)";

	public override IGameManager? GameManager
	{
		get => field ??= new WuwaGameManager(ExecutableName, ApiResponseUrl, ApiResponseAssetUrl, AuthenticationHash,
			CurrentTag, Hash1, AreApiParametersEncrypted, ThirdPartySdkFolderName, SdkMarkerFileName,
			LauncherGameDirectoryName);
		set;
	}

	public override IGameInstaller? GameInstaller
	{
		get => field ??= new WuwaGameInstaller(GameManager);
		set;
	}
}