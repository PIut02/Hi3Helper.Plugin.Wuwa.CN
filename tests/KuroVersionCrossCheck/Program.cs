using System.Text.Json;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Wuwa.Management.Api;
using Hi3Helper.Plugin.Wuwa.Utils;

// Verification harness for WuwaKuroVersionCrossCheck.TryResolve().
//
// The payloads below are byte exact copies of what was measured on this machine:
//   * mainland launcherDownloadConfig.json (top level version lags one release behind the bundles)
//   * the live mainland API reports default.config.version = 3.7.0 for the same install
//   * app-game-config.json (the plugin's own file) carries the stale 3.6.0
int failures = 0;

// --- 1. the real mainland file: stale top level field, correct bundle entry ----------------------
Check("cn-real-file",
    """{"appId":"10003","version":"3.6.0","state":"","bundles":{"HD":{"version":"3.7.0","state":"","resourcePacks":["common","hd"]}}}""",
    "3.7.0", expectSource: "bundles.HD", liveApi: "3.7.0");
CheckSyncs("cn-real-file-recovers-stale-local",
    """{"appId":"10003","version":"3.6.0","state":"","bundles":{"HD":{"version":"3.7.0","state":"","resourcePacks":["common","hd"]}}}""",
    liveApi: "3.7.0", local: "3.6.0", expectedSync: "3.7.0");

// --- 2. load time cross-check: API not initialized yet (ApiGameVersion == Empty) ----------------
Check("no-api-yet",
    """{"version":"3.6.0","bundles":{"HD":{"version":"3.7.0"}}}""",
    "3.7.0", expectSource: "bundles.HD", liveApi: null);

// --- 3. agreement without bundles: exactly the old behaviour ------------------------------------
Check("top-level-only", """{"version":"3.7.0"}""", "3.7.0", expectSource: "version", liveApi: "3.7.0");
Check("top-level-newer-than-bundle", """{"version":"3.7.0","bundles":{"HD":{"version":"3.5.3"}}}""",
    "3.7.0", expectSource: "version", liveApi: "3.7.0");
CheckNoSync("matching-versions", """{"version":"3.6.0"}""", local: "3.6.0", liveApi: "3.6.0");

// --- 4. pre-download guard: bundles above the live API version are a target, not the local build -
Check("predownload-bundle-rejected",
    """{"version":"3.7.0","bundles":{"HD":{"version":"3.8.0"}}}""",
    "3.7.0", expectSource: "version", liveApi: "3.7.0");
CheckUnresolvable("everything-above-live-api",
    """{"version":"3.8.0","bundles":{"HD":{"version":"3.8.0"}}}""", liveApi: "3.7.0");

// --- 5. multi pack determinism ------------------------------------------------------------------
Check("multi-pack-highest-wins",
    """{"version":"3.6.0","bundles":{"common":{"version":"3.7.0"},"HD":{"version":"3.7.2"}}}""",
    "3.7.2", expectSource: "bundles.HD", liveApi: "3.7.2");
Check("multi-pack-tie-keeps-first-sorted",
    """{"version":"3.6.0","bundles":{"common":{"version":"3.7.0"},"HD":{"version":"3.7.0"}}}""",
    "3.7.0", expectSource: "bundles.HD", liveApi: "3.7.0");

// --- 6. junk input must not invent a version ----------------------------------------------------
CheckUnresolvable("empty-object", "{}", liveApi: "3.7.0");
CheckUnresolvable("empty-version", """{"version":"","bundles":{"HD":{}}}""", liveApi: "3.7.0");
CheckUnresolvable("garbage-version", """{"version":"not-a-version"}""", liveApi: "3.7.0");
CheckUnresolvable("null-config", null, liveApi: "3.7.0");

// --- 7. optional take on a real file: `dotnet run -- <launcherDownloadConfig.json> [liveApiVersion]`
string[] commandLine = Environment.GetCommandLineArgs();
if (commandLine.Length > 1 && File.Exists(commandLine[1]))
{
    string liveApiText = commandLine.Length > 2 ? commandLine[2] : string.Empty;
    GameVersion liveApi = string.IsNullOrEmpty(liveApiText) ? GameVersion.Empty : new GameVersion(liveApiText);
    string json = File.ReadAllText(commandLine[1]);

    if (!WuwaKuroVersionCrossCheck.TryResolve(Parse(json), liveApi, out GameVersion version, out string source))
    {
        Console.WriteLine($"FAIL live-file {commandLine[1]}: nothing resolvable.");
        failures++;
    }
    else
    {
        Console.WriteLine($"ok   live-file {commandLine[1]}: {version} <- {source} (liveApi={(liveApi == GameVersion.Empty ? "unknown" : liveApi.ToString())})");
    }
}

if (failures > 0)
{
    Console.WriteLine($"{failures} check(s) FAILED.");
    return 1;
}

Console.WriteLine("All Kuro version cross-check checks passed.");
return 0;

WuwaLauncherDownloadConfig? Parse(string? json)
    => json == null ? null : JsonSerializer.Deserialize<WuwaLauncherDownloadConfig>(json);

void Check(string name, string? json, string expected, string expectSource, string? liveApi)
{
    GameVersion api = liveApi == null ? GameVersion.Empty : new GameVersion(liveApi);
    if (!WuwaKuroVersionCrossCheck.TryResolve(Parse(json), api, out GameVersion version, out string source))
    {
        Console.WriteLine($"FAIL {name}: expected {expected} but nothing was resolvable.");
        failures++;
        return;
    }

    if (version.ToString() != expected || source != expectSource)
    {
        Console.WriteLine($"FAIL {name}: got {version} (source={source}), expected {expected} (source={expectSource}).");
        failures++;
        return;
    }

    Console.WriteLine($"ok   {name}: {version} <- {source}");
}

void CheckUnresolvable(string name, string? json, string? liveApi)
{
    GameVersion api = liveApi == null ? GameVersion.Empty : new GameVersion(liveApi);
    if (WuwaKuroVersionCrossCheck.TryResolve(Parse(json), api, out GameVersion version, out string source))
    {
        Console.WriteLine($"FAIL {name}: expected no usable version but got {version} (source={source}).");
        failures++;
        return;
    }

    Console.WriteLine($"ok   {name}: unresolvable");
}

// Mirrors the decision the manager makes for a stale app-game-config.json.
void CheckSyncs(string name, string? json, string liveApi, string local, string expectedSync)
{
    GameVersion api = new(liveApi);
    if (!WuwaKuroVersionCrossCheck.TryResolve(Parse(json), api, out GameVersion version, out _))
    {
        Console.WriteLine($"FAIL {name}: expected {expectedSync} to be synced but nothing was resolvable.");
        failures++;
        return;
    }

    GameVersion currentVersion = new(local);
    GameVersion syncVersion = GameVersion.Empty;
    if (version > currentVersion)
        syncVersion = version;
    if (version == api && api > currentVersion)
        syncVersion = api;

    if (syncVersion.ToString() != expectedSync)
    {
        Console.WriteLine($"FAIL {name}: sync target {syncVersion}, expected {expectedSync}.");
        failures++;
        return;
    }

    Console.WriteLine($"ok   {name}: local {local} -> {syncVersion}");
}

void CheckNoSync(string name, string? json, string local, string liveApi)
{
    GameVersion api = new(liveApi);
    if (!WuwaKuroVersionCrossCheck.TryResolve(Parse(json), api, out GameVersion version, out _))
    {
        Console.WriteLine($"FAIL {name}: expected a resolvable version.");
        failures++;
        return;
    }

    GameVersion currentVersion = new(local);
    if (version > currentVersion || (version == api && api > currentVersion))
    {
        Console.WriteLine($"FAIL {name}: unexpected version sync to {version}.");
        failures++;
        return;
    }

    Console.WriteLine($"ok   {name}: no sync needed ({version})");
}
