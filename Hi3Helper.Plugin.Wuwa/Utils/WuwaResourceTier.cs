using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Hi3Helper.Plugin.Core;
using Microsoft.Extensions.Logging;

// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo

namespace Hi3Helper.Plugin.Wuwa.Utils;

/// <summary>
/// Helper for resolving the "tiered resource" (SD/HD/UHD) package that the user has
/// actually downloaded through the official Kuro launcher.
/// <br/><br/>
/// Starting from Wuthering Waves 3.7, <c>Client-Win64-Shipping.exe</c> refuses to boot
/// (fatal error: <c>kuro: Use launcher to start game!</c>) unless the client is told which
/// resource tier it should mount. The official launcher passes that information through the
/// <c>-krqlv=&lt;sd|hd|uhd&gt;</c> command line argument.
/// <br/><br/>
/// The value has to match one of the packages that exists on disk, so instead of guessing we
/// read it from the bookkeeping file the official launcher maintains in the game directory
/// (<c>launcherDownloadConfig.json</c>) and fall back to the on-disk package folders.
/// </summary>
internal static class WuwaResourceTier
{
    /// <summary>
    /// Command line switch appended by the official Kuro launcher to select the mounted resource tier.
    /// </summary>
    internal const string ResourceTierArgumentPrefix = "-krqlv=";

    private const string LauncherDownloadConfigFileName = "launcherDownloadConfig.json";
    private const string LauncherDownloadConfigDirectoryName = "launcherDownloadConfig";
    private const string LauncherDownloadDirectoryName = "launcherDownload";
    private const string BundlesPropertyName = "bundles";
    private const string PackNamePropertyName = "packName";

    /// <summary>
    /// Names of the known resource tiers, ordered from the lowest to the highest quality.
    /// </summary>
    private static readonly string[] KnownTiers = ["sd", "hd", "uhd"];

    /// <summary>
    /// Builds the final command line arguments for the game bootstrap executable by appending the
    /// resource tier switch when it can be resolved and the user did not supply one already.
    /// </summary>
    /// <param name="gamePath">The resolved game installation directory.</param>
    /// <param name="startArgument">The start argument provided by the host (may be <c>null</c>).</param>
    /// <returns>The command line arguments to pass to the game bootstrap executable.</returns>
    internal static string BuildStartArguments(string? gamePath, string? startArgument)
    {
        // A user supplied switch always wins, so we do not append a duplicated one.
        if (HasResourceTierArgument(startArgument))
        {
            SharedStatic.InstanceLogger.LogDebug(
                "[WuwaResourceTier] Resource tier argument already present in start argument, leaving it untouched: {StartArgument}",
                startArgument);
            return startArgument ?? string.Empty;
        }

        if (!TryGetInstalledTier(gamePath, out var tier))
        {
            SharedStatic.InstanceLogger.LogDebug(
                "[WuwaResourceTier] Unable to resolve the installed resource tier for '{GamePath}', skipping the -krqlv switch.",
                gamePath);
            return startArgument ?? string.Empty;
        }

        var tierArgument = ResourceTierArgumentPrefix + tier;
        var result = string.IsNullOrWhiteSpace(startArgument)
            ? tierArgument
            : startArgument!.Trim() + " " + tierArgument;

        SharedStatic.InstanceLogger.LogInformation(
            "[WuwaResourceTier] Injecting resource tier switch '{TierArgument}' for game at '{GamePath}'.",
            tierArgument, gamePath);
        return result;
    }

    /// <summary>
    /// Returns <c>true</c> when the supplied start argument already carries a <c>-krqlv</c> switch.
    /// </summary>
    internal static bool HasResourceTierArgument(string? startArgument)
    {
        if (string.IsNullOrWhiteSpace(startArgument)) return false;

        return startArgument
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(x => x.StartsWith(ResourceTierArgumentPrefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Tries to resolve the resource tier which is currently installed inside the game directory.
    /// </summary>
    internal static bool TryGetInstalledTier(string? gamePath, out string? tier)
    {
        tier = null;
        if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath)) return false;

        if (TryGetTierFromLauncherDownloadConfigFile(gamePath, out tier)) return true;
        if (TryGetTierFromLauncherDownloadConfigDirectory(gamePath, out tier)) return true;
        if (TryGetTierFromLauncherDownloadDirectory(gamePath, out tier)) return true;

        return false;
    }

    /// <summary>
    /// Reads the tier from <c>&lt;gamePath&gt;/launcherDownloadConfig.json</c>, which is the
    /// bookkeeping file written by the official launcher after every download/update.
    /// </summary>
    private static bool TryGetTierFromLauncherDownloadConfigFile(string gamePath, out string? tier)
    {
        tier = null;
        var configPath = Path.Combine(gamePath, LauncherDownloadConfigFileName);
        if (!File.Exists(configPath)) return false;

        try
        {
            var rootNode = JsonNode.Parse(File.ReadAllText(configPath));
            if (rootNode?[BundlesPropertyName] is not JsonObject bundles || bundles.Count == 0)
                return false;

            // The bundle key itself (e.g. "HD") is the tier identifier, keep the original JSON order.
            var bundleKeys = bundles.Select(x => x.Key)
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(x => x.ToLowerInvariant())
                .Where(x => KnownTiers.Contains(x, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (bundleKeys.Count == 0) return false;

            if (bundleKeys.Count > 1)
            {
                SharedStatic.InstanceLogger.LogWarning(
                    "[WuwaResourceTier] Multiple resource tiers found in '{ConfigPath}': {Tiers}. Using '{Tier}'.",
                    configPath, string.Join(", ", bundleKeys), bundleKeys[0]);
            }

            tier = bundleKeys[0];
            return true;
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogWarning(
                "[WuwaResourceTier] Failed to parse '{ConfigPath}': {Error}", configPath, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Reads the tier from the per-package manifest folder <c>&lt;gamePath&gt;/launcherDownloadConfig/*.json</c>.
    /// </summary>
    private static bool TryGetTierFromLauncherDownloadConfigDirectory(string gamePath, out string? tier)
    {
        tier = null;
        var configDir = Path.Combine(gamePath, LauncherDownloadConfigDirectoryName);
        if (!Directory.Exists(configDir)) return false;

        try
        {
            foreach (var manifestPath in Directory.EnumerateFiles(configDir, "*.json"))
            {
                var rootNode = JsonNode.Parse(File.ReadAllText(manifestPath));
                var packName = rootNode?[PackNamePropertyName]?.GetValue<string>()?.ToLowerInvariant();
                if (string.IsNullOrEmpty(packName)) continue;
                if (!KnownTiers.Contains(packName, StringComparer.OrdinalIgnoreCase)) continue;

                tier = packName;
                return true;
            }
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogWarning(
                "[WuwaResourceTier] Failed to scan '{ConfigDir}': {Error}", configDir, ex.Message);
        }

        return false;
    }

    /// <summary>
    /// Fallback: detects the tier from the package folders inside <c>&lt;gamePath&gt;/launcherDownload</c>.
    /// </summary>
    private static bool TryGetTierFromLauncherDownloadDirectory(string gamePath, out string? tier)
    {
        tier = null;
        var downloadDir = Path.Combine(gamePath, LauncherDownloadDirectoryName);
        if (!Directory.Exists(downloadDir)) return false;

        try
        {
            var installedTiers = KnownTiers
                .Where(x => Directory.Exists(Path.Combine(downloadDir, x)))
                .ToList();

            if (installedTiers.Count == 0) return false;

            // Prefer the highest available quality when more than one package is present on disk.
            tier = installedTiers[^1];
            return true;
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogWarning(
                "[WuwaResourceTier] Failed to scan '{DownloadDir}': {Error}", downloadDir, ex.Message);
            return false;
        }
    }
}
