using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.Plugin.Wuwa.Management.PresetConfig;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Hi3Helper.Plugin.Wuwa;
public partial class Exports
{
	private const string SteamLaunchUri = "steam://run/3513350"; // 3513350 is Wuthering Waves' Steam AppID
	private bool IsSteamLoading = false;
	private DateTime? SteamStartTime = null;
	private Process[] SteamProcesses = [];

	private const int SW_MINIMIZE = 6;

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

	private async Task<bool> TryInitializeSteamLauncher(GameManagerExtension.RunGameFromGameManagerContext context, CancellationToken token)
	{
		if (context.PresetConfig is not WuwaSteamPresetConfig presetConfig)
		{
			return true;
		}

		IsSteamLoading = true;
		SteamStartTime = DateTime.Now;

		// Trigger game start via Steam Client
		ProcessStartInfo psi = new()
		{
			FileName = SteamLaunchUri,
			UseShellExecute = true
		};
		Process.Start(psi);

		// Find main process for Steam
		int delay = 0;
		while (SteamProcesses.Length == 0 && delay < 15000)
		{
			SteamProcesses = Process.GetProcessesByName("Steam");

			await Task.Delay(200, token);
			delay += 200;
		}

		if (SteamProcesses.Length > 0)
		{
			Process p = SteamProcesses.First();
			while (p.MainWindowHandle == IntPtr.Zero)
			{
				p.Refresh();
				await Task.Delay(100, token);
			}

			// Minimize Steam window
			ShowWindow(p.MainWindowHandle, SW_MINIMIZE);
		}

		IsSteamLoading = false;

		return true;
	}
}
