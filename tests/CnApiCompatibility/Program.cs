// Fork-specific compatibility checks for the mainland China (国服) CDN.
//
// The mainland CDN still serves the legacy API generation while upstream's global regions moved
// to the "official" game-config endpoint. These checks pin down both shapes so a future upstream
// merge cannot silently make the mainland region talk to the global endpoints.
//
// Endpoint values below were measured against the live CDN on 2026-11-14.
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
const string cnAuth = "10003_Y8xXrXk65DqFHEDgApn3cpK5lfczpFx5";
const string cnTag = "G152";

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

Task InitMedia(LauncherApiMediaBase media) => (Task)media.GetType()
    .GetMethod("InitAsync", flags | BindingFlags.DeclaredOnly)!
    .Invoke(media, [CancellationToken.None])!;

async Task Test(string name, Func<Task> action)
{
    await action();
    passed++;
    Console.WriteLine("PASS " + name);
}

try
{
    await Test("CN and global regions use different game-config endpoints", () =>
    {
        var cn = new WuwaCnPresetConfig();
        var global = new WuwaGlobalPresetConfig();

        string? cnUrl = (string?)Get(cn.GameManager!, "GameConfigUrl");
        string? globalUrl = (string?)Get(global.GameManager!, "GameConfigUrl");

        Assert(cnUrl == $"{cnBase}launcher/game/{cnTag}/{cnAuth}/index.json",
            "CN manager must use the CN game-config URL, got: " + cnUrl);
        Assert(globalUrl!.StartsWith("https://prod-alicdn-gamestarter.kurogame.com/", StringComparison.Ordinal) &&
               globalUrl.EndsWith("/official/index.json", StringComparison.Ordinal),
            "Global manager must use the official global endpoint, got: " + globalUrl);
        Assert(cnUrl != globalUrl, "CN and global must not share a game-config endpoint");

        // The CN CDN must never be handed the global-only "official" path.
        Assert(!cnUrl!.Contains("official", StringComparison.Ordinal),
            "CN game-config must not contain the global 'official' segment");
        return Task.CompletedTask;
    });

    await Test("launcher-config model accepts both legacy root and global nested shapes", () =>
    {
        var legacy = JsonSerializer.Deserialize(
            """{"functionCode":{"background":"cn-hash"}}""",
            WuwaApiResponseContext.Default.WuwaApiResponseLauncherConfig);
        var modern = JsonSerializer.Deserialize(
            """{"config":{"functionCode":{"background":"global-hash"}}}""",
            WuwaApiResponseContext.Default.WuwaApiResponseLauncherConfig);

        Assert(legacy?.Config == null && legacy?.FunctionCode?.Background == "cn-hash",
            "Legacy CN launcher-config shape failed to deserialize");
        Assert(modern?.FunctionCode == null && modern?.Config?.FunctionCode?.Background == "global-hash",
            "New global game-config shape failed to deserialize");
        return Task.CompletedTask;
    });

    await Test("CN game-config keeps the legacy default.config shape without resource packs", () =>
    {
        // Trimmed representative of the live CN response (root keys: default, keyFileCheckList,
        // fingerprints, ... ; default.version measured as 3.7.1 on 2026-11-14).
        const string cnGameConfig = """
            {"default":{"cdnList":[],"config":{"indexFile":"current.json","indexFileMd5":"abc",
            "version":"3.7.1","patchType":"0","size":1,"baseUrl":"base"},"version":"3.7.1"},
            "keyFileCheckList":["Wuthering Waves.exe"],"fingerprints":[]}
            """;
        var cfg = JsonSerializer.Deserialize(cnGameConfig,
            WuwaApiResponseContext.Default.WuwaApiResponseGameConfig);

        Assert(cfg?.Default?.ConfigReference != null,
            "CN default.config must still map to Default.ConfigReference");
        Assert(cfg?.Default?.ConfigReference?.CurrentVersion.ToString() == "3.7.1",
            "CN game-config version was lost");
        Assert(cfg?.ResourcePacks == null && cfg?.Bundles == null,
            "CN game-config must not be mistaken for a resource-pack config");
        return Task.CompletedTask;
    });

    await Test("CN media uses legacy launcher-config and zh-Hans wallpaper URL", async () =>
    {
        string configUrl = $"{cnBase}launcher/launcher/{cnAuth}/{cnTag}/index.json";
        string wallpaperUrl = $"{cnBase}launcher/{cnAuth}/{cnTag}/background/cn-hash/zh-Hans.json";
        var requests = new List<string>();

        using var client = new HttpClient(new Handler(request =>
        {
            string url = request.RequestUri!.AbsoluteUri;
            requests.Add(url);
            if (url == configUrl)
                return Json("""{"functionCode":{"background":"cn-hash"}}""");
            if (url == wallpaperUrl)
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
        await InitMedia(media);

        Assert(requests.Count == 2 && requests[0] == configUrl && requests[1] == wallpaperUrl,
            "CN media must request the CN launcher-config and zh-Hans wallpaper: " +
            string.Join(", ", requests));

        using var backgrounds = PluginDisposableMemoryExtension
            .ToManagedSpan<LauncherPathEntry>(media.GetBackgroundEntries);
        using LauncherPathEntry background = backgrounds[0];
        Assert(background.Path == "https://assets.test/bg.webp", "CN background was lost");

        using var logos = PluginDisposableMemoryExtension
            .ToManagedSpan<LauncherPathEntry>(media.GetLogoOverlayEntries);
        using LauncherPathEntry logo = logos[0];
        Assert(logo.Path == "https://assets.test/slogan.png", "CN slogan was lost");
        media.Dispose();
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
