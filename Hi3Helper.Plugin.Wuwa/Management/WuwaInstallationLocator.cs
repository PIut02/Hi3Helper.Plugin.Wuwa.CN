using System;
using System.Collections.Generic;
using System.IO;
using Hi3Helper.Plugin.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo

// The plugin is only ever loaded by Collapse Launcher, which is a Windows-only application, so
// the Windows registry API is used unconditionally.
#pragma warning disable CA1416 // Validate platform compatibility

namespace Hi3Helper.Plugin.Wuwa.Management;

/// <summary>
/// Locates an already existing Wuthering Waves installation on the machine.
/// <br/>
/// The official Kuro launcher records where it installed itself in the registry, and the game
/// itself always lives in a folder called <c>Wuthering Waves Game</c> right next to it, so those
/// records are the most reliable source. Well-known default locations are checked as a fallback.
/// </summary>
internal static class WuwaInstallationLocator
{
    /// <summary>
    /// Folder name used by the official Kuro launcher on the drive it is installed to.
    /// </summary>
    private const string LauncherRootFolderName = "Wuthering Waves";

    /// <summary>
    /// Registry key holding the installation root recorded by the official Kuro launcher.
    /// </summary>
    private const string KuroLauncherRegistryKeyPath = @"Software\kurogame\KRLauncher";

    /// <summary>
    /// Registry value written by the official Kuro launcher pointing to its own installation folder.
    /// </summary>
    private const string KuroLauncherInstallPathValueName = "SingleLauncherInstallPath";

    /// <summary>
    /// Relative path of the game engine executable, used to validate a game installation candidate.
    /// </summary>
    private const string EngineExecutableRelativePath = @"Client\Binaries\Win64\Client-Win64-Shipping.exe";

    /// <summary>
    /// Enumerates every plausible game installation directory, in order of confidence.
    /// </summary>
    /// <param name="gameDirectoryName">Name of the game folder inside the launcher root.</param>
    internal static IEnumerable<string> EnumerateCandidatePaths(string gameDirectoryName)
    {
        // 1. Installation roots recorded by the official Kuro launcher in the registry.
        foreach (string? launcherRootPath in EnumerateKuroLauncherRootPaths())
        {
            if (string.IsNullOrEmpty(launcherRootPath)) continue;

            yield return Path.Combine(launcherRootPath, gameDirectoryName);

            // Some setups keep the game right next to the launcher executable.
            yield return launcherRootPath;
        }

        // 2. Well-known default locations on every fixed drive.
        foreach (string candidatePath in EnumerateWellKnownInstallPaths(gameDirectoryName))
            yield return candidatePath;
    }

    /// <summary>
    /// Checks whether the given path really is a Wuthering Waves game directory, by requiring both
    /// the bootstrap executable and the engine executable to be present.
    /// </summary>
    internal static bool IsValidGameInstallPath(string? candidatePath, string bootstrapExecutableName)
    {
        if (string.IsNullOrEmpty(candidatePath) || !Directory.Exists(candidatePath)) return false;

        string bootstrapExecutablePath = Path.Combine(candidatePath, bootstrapExecutableName);
        string engineExecutablePath = Path.Combine(candidatePath, EngineExecutableRelativePath);

        return File.Exists(bootstrapExecutablePath) && File.Exists(engineExecutablePath);
    }

    /// <summary>
    /// Reads the launcher installation roots from the registry records written by the official
    /// Kuro launcher.
    /// </summary>
    private static IEnumerable<string> EnumerateKuroLauncherRootPaths()
    {
        List<string> results = [];

        CollectFromRegistryHive(Registry.CurrentUser, KuroLauncherRegistryKeyPath, results);
        CollectFromRegistryHive(Registry.LocalMachine, KuroLauncherRegistryKeyPath, results);
        CollectFromRegistryHive(Registry.LocalMachine, @"Software\WOW6432Node\kurogame\KRLauncher", results);

        // Fallback: the "KuroLauncher.<appId>" URL protocol registration stores the launcher path.
        CollectFromLauncherProtocolRegistration(results);

        return results;
    }

    private static void CollectFromRegistryHive(RegistryKey hive, string subKeyPath, List<string> results)
    {
        try
        {
            using RegistryKey? rootKey = hive.OpenSubKey(subKeyPath);
            if (rootKey == null) return;

            foreach (string subKeyName in rootKey.GetSubKeyNames())
            {
                using RegistryKey? subKey = rootKey.OpenSubKey(subKeyName);
                if (subKey?.GetValue(KuroLauncherInstallPathValueName) is not string installPath) continue;

                AddCandidatePath(results, installPath);
            }
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogDebug(
                "[WuwaInstallationLocator] Failed to read '{SubKeyPath}' from {Hive}: {Error}",
                subKeyPath, hive.Name, ex.Message);
        }
    }

    private static void CollectFromLauncherProtocolRegistration(List<string> results)
    {
        try
        {
            using RegistryKey? classesKey = Registry.LocalMachine.OpenSubKey(@"Software\Classes");
            if (classesKey == null) return;

            foreach (string subKeyName in classesKey.GetSubKeyNames())
            {
                // e.g. "KuroLauncher.10003"
                if (!subKeyName.StartsWith("KuroLauncher.", StringComparison.OrdinalIgnoreCase)) continue;

                using RegistryKey? subKey = classesKey.OpenSubKey(subKeyName);
                using RegistryKey? defaultIconKey = subKey?.OpenSubKey("DefaultIcon");
                if (defaultIconKey?.GetValue(string.Empty) is not string launcherExecutablePath) continue;

                // The value looks like: C:\Program Files\Wuthering Waves\launcher.exe
                string? launcherRootPath = Path.GetDirectoryName(launcherExecutablePath.Trim().Trim('"'));
                AddCandidatePath(results, launcherRootPath);
            }
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogDebug(
                "[WuwaInstallationLocator] Failed to read the launcher protocol registration: {Error}",
                ex.Message);
        }
    }

    private static void AddCandidatePath(List<string> results, string? candidatePath)
    {
        if (string.IsNullOrWhiteSpace(candidatePath)) return;

        string normalizedPath = candidatePath.Trim().Trim('"').TrimEnd('\\', '/');
        if (normalizedPath.Length == 0) return;
        if (results.Exists(x => string.Equals(x, normalizedPath, StringComparison.OrdinalIgnoreCase))) return;

        results.Add(normalizedPath);
    }

    private static IEnumerable<string> EnumerateWellKnownInstallPaths(string gameDirectoryName)
    {
        foreach (string driveRootPath in EnumerateFixedDriveRoots())
        {
            yield return Path.Combine(driveRootPath, "Program Files", LauncherRootFolderName, gameDirectoryName);
            yield return Path.Combine(driveRootPath, "Program Files (x86)", LauncherRootFolderName, gameDirectoryName);
            yield return Path.Combine(driveRootPath, LauncherRootFolderName, gameDirectoryName);
            yield return Path.Combine(driveRootPath, "Games", LauncherRootFolderName, gameDirectoryName);
            yield return Path.Combine(driveRootPath, "Kuro Games", LauncherRootFolderName, gameDirectoryName);
            yield return Path.Combine(driveRootPath, gameDirectoryName);
        }
    }

    private static IEnumerable<string> EnumerateFixedDriveRoots()
    {
        DriveInfo[] drives;
        try
        {
            drives = DriveInfo.GetDrives();
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogDebug(
                "[WuwaInstallationLocator] Failed to enumerate drives: {Error}", ex.Message);
            yield break;
        }

        foreach (DriveInfo drive in drives)
        {
            string? rootPath = null;
            try
            {
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
                rootPath = drive.RootDirectory.FullName;
            }
            catch
            {
                // Ignore drives which cannot be probed (e.g. disconnected network mounts).
            }

            if (!string.IsNullOrEmpty(rootPath)) yield return rootPath;
        }
    }
}
