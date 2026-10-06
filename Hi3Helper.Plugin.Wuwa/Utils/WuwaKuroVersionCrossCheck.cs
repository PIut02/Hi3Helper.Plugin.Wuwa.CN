using System;
using System.Collections.Generic;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Wuwa.Management.Api;

namespace Hi3Helper.Plugin.Wuwa.Utils;

/// <summary>
/// Resolves the game version which Kuro's <c>launcherDownloadConfig.json</c> actually reports for the
/// installed build.
/// <br/><br/>
/// That file carries two independent kinds of version fields: a top level <c>version</c> and one entry
/// per resource bundle (<c>bundles.&lt;pack&gt;.version</c>). Measured on the mainland client the top level
/// field can lag one whole release behind the content which is really on disk — top level reports
/// <c>3.6.0</c> while <c>bundles.HD.version</c> and the live API both report <c>3.7.0</c> — so the
/// highest reported value wins instead of trusting the top level field alone.
/// <br/><br/>
/// Values which sit above the live API version are rejected, because those belong to a pre-download
/// target rather than to the build the user actually has.
/// </summary>
internal static class WuwaKuroVersionCrossCheck
{
    /// <summary>The version field name which is used when the top level <c>version</c> field wins.</summary>
    internal const string TopLevelSource = "version";

    /// <summary>
    /// Picks the most plausible version out of <paramref name="config"/>.
    /// </summary>
    /// <param name="config">Parsed <c>launcherDownloadConfig.json</c>, may be <see langword="null"/>.</param>
    /// <param name="liveApiVersion">
    /// Version reported by the live API, or <see cref="GameVersion.Empty"/> when it is not known yet
    /// (e.g. while loading the config, before the API has been initialized).
    /// </param>
    /// <param name="version">The resolved version, or <see cref="GameVersion.Empty"/> when unresolvable.</param>
    /// <param name="source">Human readable origin of <paramref name="version"/>, for logging.</param>
    /// <returns><see langword="true"/> when a usable version was found.</returns>
    internal static bool TryResolve(WuwaLauncherDownloadConfig? config,
                                    GameVersion liveApiVersion,
                                    out GameVersion version,
                                    out string source)
    {
        version = GameVersion.Empty;
        source = string.Empty;

        if (config == null)
            return false;

        if (TryParse(config.Version, out GameVersion topLevelVersion))
        {
            version = topLevelVersion;
            source = TopLevelSource;
        }

        if (config.Bundles != null)
        {
            // Deterministic order, so a tie always reports the same source.
            List<string> packNames = new(config.Bundles.Keys);
            packNames.Sort(StringComparer.Ordinal);

            foreach (string packName in packNames)
            {
                if (!config.Bundles.TryGetValue(packName, out WuwaLauncherDownloadConfigBundle? bundle) ||
                    !TryParse(bundle?.Version, out GameVersion bundleVersion))
                {
                    continue;
                }

                if (bundleVersion > version)
                {
                    version = bundleVersion;
                    source = $"bundles.{packName}";
                }
            }
        }

        if (version == GameVersion.Empty)
            return false;

        // No API knowledge yet (the load time cross-check runs before the API is initialized) or the
        // value is within the live version: nothing else to check.
        if (liveApiVersion == GameVersion.Empty || version <= liveApiVersion)
            return true;

        // Above the live API version: that is a bundle bump for a pre-download target. Fall back to
        // the top level field, which matches the behaviour from before bundles were considered.
        if (source != TopLevelSource &&
            TryParse(config.Version, out GameVersion fallbackVersion) &&
            fallbackVersion <= liveApiVersion)
        {
            version = fallbackVersion;
            source = TopLevelSource;
            return true;
        }

        version = GameVersion.Empty;
        source = string.Empty;
        return false;
    }

    private static bool TryParse(string? value, out GameVersion version)
    {
        version = GameVersion.Empty;

        return !string.IsNullOrWhiteSpace(value) &&
               GameVersion.TryParse(value, null, out version) &&
               version != GameVersion.Empty;
    }
}
