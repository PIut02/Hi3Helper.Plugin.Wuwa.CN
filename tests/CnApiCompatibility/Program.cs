// Fork-specific regression checks for the mainland China (国服) preset.
//
// After the 2026-11-14 upgrade both regions use the SAME official API shape:
//   /launcher/game/{appId}_{appKey}/{gameId}/official/index.json
//   /launcher/app /{appId}_{appKey}/{gameId}/official/{social|information}/{locale}.json
//   /launcher/game/{appId}_{appKey}/{gameId}/official/background/{hash}/{locale}.json
// Only the constants differ (host, appKey, gameId, locale). This file pins those constants
// down and makes sure no region-specific URL construction sneaks back in.
//
// Values were read from the official launcher's kr_get_launcher_conf callback (KRLauncher log,
// XOR 0x09) and verified against the live CDN on 2026-11-14.
using System.Net;
using System.Reflection;
using System.Text.Json;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management.Api;
using Hi3Helper.Plugin.Wuwa.Management.Api;
using Hi3Helper.Plugin.Wuwa.Management.PresetConfig;

const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                           BindingFlags.Instance | BindingFlags.Static;

const string cnBase = "https://prod-cn-alicdn-gamestarter.kurogame.com/";
const string cnAuth = "10003_oLNgHF1CESo51DGHN2odtp40e3oI1HfZ";
const string cnGameId = "G152";
const string serverCode = "official";
const string cnLocale = "zh-Hans.json";

int passed = 0;

void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json) };

object? Get(object instance, string name)
{
    for (Type? type = instance.GetType(); type != null; type = type.BaseType)
    {
        PropertyInfo? property = type.GetProperty(name, flags | BindingFlags.DeclaredOnly);
        if (property != null) return property.GetValue(instance);
        FieldInfo? field = type.GetField(name, flags | BindingFlags.DeclaredOnly);
        if (field != null) return field.GetValue(instance);
    }
    throw new Exception("Missing member: " + name);
}

void Set(object instance, string name, object? value)
{
    for (Type? type = instance.GetType(); type != null; type = type.BaseType)
    {
        PropertyInfo? property = type.GetProperty(name, flags | BindingFlags.DeclaredOnly);
        if (property != null) { property.SetValue(instance, value); return; }
        FieldInfo? field = type.GetField(name, flags | BindingFlags.DeclaredOnly);
        if (field != null) { field.SetValue(instance, value); return; }
    }
    throw new Exception("Missing member: " + name);
}

Task InitApi(object api, string method) => (Task)api.GetType()
    .GetMethod(method, flags | BindingFlags.DeclaredOnly)!
    .Invoke(api, [CancellationToken.None])!;

async Task Test(string name, Func<Task> action)
{
    await action();
    passed++;
    Console.WriteLine("PASS " + name);
}

string cnGameConfigUrl = $"{cnBase}launcher/game/{cnAuth}/{cnGameId}/{serverCode}/index.json";
string cnSocialUrl = $"{cnBase}launcher/app/{cnAuth}/{cnGameId}/{serverCode}/social/{cnLocale}";
string cnNewsUrl = $"{cnBase}launcher/app/{cnAuth}/{cnGameId}/{serverCode}/information/{cnLocale}";
string cnWallpaperUrl = $"{cnBase}launcher/game/{cnAuth}/{cnGameId}/{serverCode}/background/cn-hash/{cnLocale}";

try
{
    await Test("CN and global share one game-config shape, only constants differ", () =>
    {
        var cn = new WuwaCnPresetConfig();
        var global = new WuwaGlobalPresetConfig();
        string? cnUrl = (string?)Get(cn.GameManager!, "GameConfigUrl");
        string? globalUrl = (string?)Get(global.GameManager!, "GameConfigUrl");

        Assert(cnUrl == cnGameConfigUrl, "CN game-config URL drifted: " + cnUrl);
        Assert(globalUrl!.StartsWith("https://prod-alicdn-gamestarter.kurogame.com/", StringComparison.Ordinal),
            "Global game-config host drifted: " + globalUrl);
        Assert(cnUrl != globalUrl, "CN and global must not share one endpoint");
        Assert(cnUrl!.EndsWith($"/{cnGameId}/{serverCode}/index.json", StringComparison.Ordinal) &&
               globalUrl.EndsWith($"/G153/{serverCode}/index.json", StringComparison.Ordinal),
            "Both regions must use the same /launcher/game/{auth}/{gameId}/official/index.json shape");
        Assert(!cnUrl.Contains("Y8xXrXk65", StringComparison.Ordinal),
            "CN still uses the frozen legacy appKey");
        return Task.CompletedTask;
    });

    await Test("launcher-config model uses the single config.functionCode shape", () =>
    {
        var official = JsonSerializer.Deserialize(
            """{"config":{"functionCode":{"background":"cn-hash"}}}""",
            WuwaApiResponseContext.Default.WuwaApiResponseLauncherConfig);
        Assert(official?.Config?.FunctionCode?.Background == "cn-hash",
            "config.functionCode.background failed to deserialize");

        // The legacy root-level functionCode is deliberately no longer mapped: it belonged to
        // the retired launcher-config channel and must not be revived.
        var legacy = JsonSerializer.Deserialize(
            """{"functionCode":{"background":"stale-hash"}}""",
            WuwaApiResponseContext.Default.WuwaApiResponseLauncherConfig);
        Assert(legacy?.Config == null, "Legacy root functionCode unexpectedly mapped again");
        return Task.CompletedTask;
    });

    await Test("CN game-config is the official resource-pack shape", () =>
    {
        const string config = """
            {"cdnList":[{"url":"https://pcdownload-aliyun.aki-game.com/"}],
             "bundles":{"HD":{"resourcePacks":["common","hd"]}},
             "resourcePacks":{"common":{"version":"3.8.0","indexFile":"common.json"},
                              "hd":{"version":"3.8.0","indexFile":"hd.json"}},
             "config":{"functionCode":{"background":"cn-hash"}},
             "resourcesGray":null,"predownload":null}
            """;
        var cfg = JsonSerializer.Deserialize(config,
            WuwaApiResponseContext.Default.WuwaApiResponseGameConfig);
        Assert(cfg?.ResourcePacks is { Count: > 0 } && cfg.Bundles is { Count: > 0 },
            "CN resource packs / bundles were lost");
        Assert(cfg?.ResourcePacks?["hd"].CurrentVersion.ToString() == "3.8.0",
            "CN game version was lost");
        return Task.CompletedTask;
    });

    await Test("CN media uses the official game-config URL and zh-Hans wallpaper", async () =>
    {
        var requests = new List<string>();
        using var client = new HttpClient(new Handler(request =>
        {
            string url = request.RequestUri!.AbsoluteUri;
            // The optional logo-overlay composition downloads the slogan artwork through the
            // download client. Answer it locally (with bytes that are not a PNG, so composing
            // gracefully falls back) and keep it out of the API request list.
            if (url == "https://assets.test/slogan.png")
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([1, 2, 3])
                };
            requests.Add(url);
            if (url == cnGameConfigUrl)
                return Json("""{"config":{"functionCode":{"background":"cn-hash"}}}""");
            if (url == cnWallpaperUrl)
                return Json("""
                    {"functionSwitch":1,"backgroundFileType":1,
                     "backgroundFile":"https://assets.test/bg.webp",
                     "firstFrameImage":"https://assets.test/preview.webp",
                     "slogan":"https://assets.test/slogan.png"}
                    """);
            throw new Exception("Unexpected CN API request: " + url);
        }));

        var media = (LauncherApiMediaBase)new WuwaCnPresetConfig().LauncherApiMedia!;
        Set(media, "ApiResponseHttpClient", client);
        Set(media, "ApiDownloadHttpClient", client);
        await InitApi(media, "InitAsync");

        Assert(requests.Count == 2 && requests[0] == cnGameConfigUrl && requests[1] == cnWallpaperUrl,
            "CN media must request the official game-config and zh-Hans wallpaper: " +
            string.Join(", ", requests));

        using var backgrounds = PluginDisposableMemoryExtension
            .ToManagedSpan<LauncherPathEntry>(media.GetBackgroundEntries);
        using LauncherPathEntry background = backgrounds[0];
        Assert(background.Path == "https://assets.test/bg.webp", "CN background was lost");
        media.Dispose();
    });

    await Test("CN news/social use the official /launcher/app/.../official/ layout", async () =>
    {
        var requests = new List<string>();
        using var client = new HttpClient(new Handler(request =>
        {
            string url = request.RequestUri!.AbsoluteUri;
            requests.Add(url);
            if (url == cnSocialUrl)
                return Json("""{"switch":1,"data":[{"name":"库街区","icon":"https://assets.test/icon.png"}]}""");
            if (url == cnNewsUrl)
                return Json("""{"guidance":{"desc":""},"slideshow":[]}""");
            throw new Exception("Unexpected CN API request: " + url);
        }));

        var news = (LauncherApiNewsBase)new WuwaCnPresetConfig().LauncherApiNews!;
        Set(news, "ApiResponseHttpClient", client);
        await InitApi(news, "InitAsync");

        Assert(requests.Count == 2 && requests.Contains(cnSocialUrl) && requests.Contains(cnNewsUrl),
            "CN news/social must use /launcher/app/{auth}/G152/official/...: " +
            string.Join(", ", requests));
        news.Dispose();
    });

    Console.WriteLine($"{passed} CN API compatibility checks passed.");
}
catch (Exception ex)
{
    Console.Error.WriteLine("FAIL: " + ex.Message);
    Environment.ExitCode = 1;
}

sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        => Task.FromResult(respond(request));
}
