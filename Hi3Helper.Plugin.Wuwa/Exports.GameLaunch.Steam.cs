using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.Plugin.Wuwa.Management.PresetConfig;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Hi3Helper.Plugin.Wuwa;
public partial class Exports
{
	private const string SteamLaunchUri = "steam://nav/games/details/3513350"; // Wuthering Waves Steam AppID, game launches through Collapse and is picked up by Steam
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

		SharedStatic.InstanceLogger.LogDebug($"Initializing Steam Launcher...");

		IsSteamLoading = true;
		SteamStartTime = DateTime.Now;

		// Trigger game start via Steam Client
		ProcessStartInfo psi = new()
		{
			FileName = SteamLaunchUri,
			UseShellExecute = true
		};
		Process.Start(psi);

		SharedStatic.InstanceLogger.LogDebug($"Started Steam process...");

		// Find main process for Steam
		int delay = 0;
		while (SteamProcesses.Length == 0 && delay < 15000)
		{
			SteamProcesses = Process.GetProcessesByName("Steam");
#if DEBUG
			SharedStatic.InstanceLogger.LogDebug($"Searching for Steam process... Found {SteamProcesses.Length} processes.");
#endif
			await Task.Delay(10000, token); // Yes this is required because Steam is slow to start
			delay += 10000;
		}

		if (SteamProcesses.Length > 0)
		{
			foreach (Process p1 in SteamProcesses)
			{
				p1.Refresh();
#if DEBUG
				SharedStatic.InstanceLogger.LogDebug($"Checking Steam process ID {p1.Id} for main window handle...");
#endif
				if (p1.MainWindowHandle != IntPtr.Zero)
				{
#if DEBUG
					SharedStatic.InstanceLogger.LogDebug($"Found Steam main window handle: {p1.MainWindowHandle}");
#endif
					while (!WaitForMainHandle(p1, token).Result)
					{
						await Task.Delay(200, token);
					}
					break;
				}
			}
		}

		async static Task<bool> WaitForMainHandle(Process p, CancellationToken token)
		{
			while (p.MainWindowHandle == IntPtr.Zero)
			{
				p.Refresh();
				await Task.Delay(200, token);
			}
			// Minimize Steam window
			ShowWindow(p.MainWindowHandle, SW_MINIMIZE);

			return true;
		}

		IsSteamLoading = false;
#if DEBUG
		SharedStatic.InstanceLogger.LogDebug($"Steam should be done loading by now...");
#endif

		return true;
	}
}
