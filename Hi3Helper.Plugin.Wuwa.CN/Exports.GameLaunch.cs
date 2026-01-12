using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Plugin.Core.Management.PresetConfig;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.Plugin.Wuwa.CN.Management;
using Hi3Helper.Plugin.Wuwa.CN.Management.PresetConfig;
using Microsoft.Extensions.Logging;

namespace Hi3Helper.Plugin.Wuwa.CN;

public partial class Exports
{
	/// <inheritdoc />
	protected override (bool IsSupported, Task<bool> Task) LaunchGameFromGameManagerCoreAsync(
        GameManagerExtension.RunGameFromGameManagerContext context, string? startArgument, bool isRunBoosted,
        ProcessPriorityClass processPriority, CancellationToken token)
    {
        return (true, Impl());

        async Task<bool> Impl()
        {
            if (!await TryInitializeEpicLauncher(context, token)) return false;

            if (!await TryInitializeSteamLauncher(context, token)) return false;

            if (!TryGetStartingProcessFromContext(context, startArgument, out var process)) return false;

            using (process)
            {
                process.Start();

                try
                {
                    process.PriorityBoostEnabled = isRunBoosted;
                    process.PriorityClass = processPriority;
                }
                catch (Exception e)
                {
                    InstanceLogger.LogError(e,
                        "[Wuwa::LaunchGameFromGameManagerCoreAsync()] An error has occurred while trying to set process priority, Ignoring!");
                }

                CancellationTokenSource gameLogReaderCts = new();
                var coopCts = CancellationTokenSource.CreateLinkedTokenSource(token, gameLogReaderCts.Token);

                if (!TryGetGameProcessFromContext(context, out var gameProcess)) return false;

                // Run game log reader (Create a new thread)
                _ = ReadGameLog(context, coopCts.Token);

                _ = TryKillEpicLauncher(context, token);

                // ReSharper disable once PossiblyMistakenUseOfCancellationToken
                await process.WaitForExitAsync(token);
                await gameLogReaderCts.CancelAsync();

                gameProcess.Dispose();
                return true;
            }
        }
    }

	/// <inheritdoc />
	protected override bool IsGameRunningCore(GameManagerExtension.RunGameFromGameManagerContext context,
        out bool isGameRunning, out DateTime gameStartTime)
    {
        isGameRunning = false;
        gameStartTime = default;

        string? startingExecutablePath = null;
        string? gameExecutablePath = null;
        if (!TryGetStartingExecutablePath(context, out startingExecutablePath)
            && !TryGetStartingExecutablePath(context, out gameExecutablePath))
            return true;

        using var process = FindExecutableProcess(startingExecutablePath);
        using var gameProcess = FindExecutableProcess(gameExecutablePath);
        isGameRunning = process != null || gameProcess != null || IsEpicLoading || IsSteamLoading;
        gameStartTime = process?.StartTime ?? gameProcess?.StartTime ?? EpicStartTime ?? SteamStartTime ?? default;

        return true;
    }

	/// <inheritdoc />
	protected override (bool IsSupported, Task<bool> Task) WaitRunningGameCoreAsync(
        GameManagerExtension.RunGameFromGameManagerContext context, CancellationToken token)
    {
        return (true, Impl());

        async Task<bool> Impl()
        {
            while (IsEpicLoading) await Task.Delay(200, token);

            while (IsSteamLoading) await Task.Delay(200, token);

            string? startingExecutablePath = null;
            string? gameExecutablePath = null;
            if (!TryGetStartingExecutablePath(context, out startingExecutablePath)
                && !TryGetStartingExecutablePath(context, out gameExecutablePath))
                return true;

            using var process = FindExecutableProcess(startingExecutablePath);
            using var gameProcess = FindExecutableProcess(gameExecutablePath);

            if (gameProcess != null)
                await gameProcess.WaitForExitAsync(token);
            else if (process != null)
                await process.WaitForExitAsync(token);

            return true;
        }
    }

	/// <inheritdoc />
	protected override bool KillRunningGameCore(GameManagerExtension.RunGameFromGameManagerContext context,
        out bool wasGameRunning, out DateTime gameStartTime)
    {
        wasGameRunning = false;
        gameStartTime = default;

        if (!TryGetGameExecutablePath(context, out var gameExecutablePath)) return true;

        using var process = FindExecutableProcess(gameExecutablePath);
        if (process == null) return true;

        wasGameRunning = true;
        gameStartTime = process.StartTime;
        process.Kill();
        return true;
    }

    private static Process? FindExecutableProcess(string? executablePath)
    {
        if (executablePath == null) return null;

        var executableDirPath = Path.GetDirectoryName(executablePath.AsSpan());
        var executableName = Path.GetFileNameWithoutExtension(executablePath);

        var processes = Process.GetProcessesByName(executableName);
        Process? returnProcess = null;

        foreach (var process in processes)
            if (process.MainModule?.FileName.StartsWith(executableDirPath, StringComparison.OrdinalIgnoreCase) ?? false)
            {
                returnProcess = process;
                break;
            }

        try
        {
            return returnProcess;
        }
        finally
        {
            foreach (var process in processes.Where(x => x != returnProcess)) process.Dispose();
        }
    }

    private static bool TryGetGameExecutablePath(GameManagerExtension.RunGameFromGameManagerContext context,
        [NotNullWhen(true)] out string? gameExecutablePath)
    {
        gameExecutablePath = null;
        if (context is not
            {
                GameManager: WuwaGameManager dnaGameManager, PresetConfig: PluginPresetConfigBase presetConfig
            }) return false;

        dnaGameManager.GetGamePath(out var gamePath);
        presetConfig.comGet_GameExecutableName(out var executablePath);

        gamePath?.NormalizePathInplace();
        executablePath.NormalizePathInplace();

        if (string.IsNullOrEmpty(gamePath)) return false;

        gameExecutablePath = Path.Combine(gamePath, executablePath);
        return File.Exists(gameExecutablePath);
    }

    private static bool TryGetGameProcessFromContext(GameManagerExtension.RunGameFromGameManagerContext context,
        [NotNullWhen(true)] out Process? process)
    {
        process = null;
        if (!TryGetGameExecutablePath(context, out var gameExecutablePath)) return false;

        var startInfo = new ProcessStartInfo(gameExecutablePath);

        process = new Process
        {
            StartInfo = startInfo
        };
        return true;
    }

    private static bool TryGetStartingExecutablePath(GameManagerExtension.RunGameFromGameManagerContext context,
        [NotNullWhen(true)] out string? startingExecutablePath)
    {
        startingExecutablePath = null;
        if (context is not
            { GameManager: WuwaGameManager dnaGameManager, PresetConfig: WuwaPresetConfig presetConfig }) return false;

        dnaGameManager.GetGamePath(out var gamePath);
        var executablePath = presetConfig?.StartExecutableName;

        gamePath?.NormalizePathInplace();
        executablePath?.NormalizePathInplace();

        if (string.IsNullOrEmpty(gamePath)
            || string.IsNullOrEmpty(executablePath))
            return false;

        startingExecutablePath = Path.Combine(gamePath, executablePath);
        return File.Exists(startingExecutablePath);
    }

    private static bool TryGetStartingProcessFromContext(GameManagerExtension.RunGameFromGameManagerContext context,
        string? startArgument, [NotNullWhen(true)] out Process? process)
    {
        process = null;
        if (!TryGetStartingExecutablePath(context, out var startingExecutablePath)) return false;

        var startInfo = string.IsNullOrEmpty(startArgument)
            ? new ProcessStartInfo(startingExecutablePath)
            : new ProcessStartInfo(startingExecutablePath, startArgument);

        process = new Process
        {
            StartInfo = startInfo
        };
        return true;
    }

    private static async Task ReadGameLog(GameManagerExtension.RunGameFromGameManagerContext context,
        CancellationToken token)
    {
        if (context is not { PresetConfig: PluginPresetConfigBase presetConfig }) return;

        presetConfig.comGet_GameAppDataPath(out var gameAppDataPath);
        presetConfig.comGet_GameLogFileName(out var gameLogFileName);

        if (string.IsNullOrEmpty(gameAppDataPath) ||
            string.IsNullOrEmpty(gameLogFileName))
            return;

        var gameLogPath = Path.Combine(gameAppDataPath, gameLogFileName);
        await Task.Delay(250, token);

        var retry = 5;
        while (!File.Exists(gameLogPath) && retry >= 0)
        {
            // Delays for 5 seconds to wait the game log existence
            await Task.Delay(1000, token);
            --retry;
        }

        if (retry <= 0) return;

        var printCallback = context.PrintGameLogCallback;

        await using var fileStream =
            File.Open(gameLogPath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fileStream);

        fileStream.Position = 0;
        while (!token.IsCancellationRequested)
        {
            while (await reader.ReadLineAsync(token) is { } line) PassStringLineToCallback(printCallback, line);

            await Task.Delay(250, token);
        }

        return;

        static unsafe void PassStringLineToCallback(GameManagerExtension.PrintGameLog? invoke, string line)
        {
            var lineP = line.GetPinnableStringPointer();
            var lineLen = line.Length;

            invoke?.Invoke(lineP, lineLen, 0);
        }
    }
}