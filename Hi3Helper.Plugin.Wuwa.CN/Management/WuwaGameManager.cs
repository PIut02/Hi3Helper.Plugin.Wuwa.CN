using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices.Marshalling;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.Plugin.Core.Utility.Json;
using Hi3Helper.Plugin.Wuwa.CN.Management.Api;
using Hi3Helper.Plugin.Wuwa.CN.Utils;
using Microsoft.Extensions.Logging;

// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypos

namespace Hi3Helper.Plugin.Wuwa.CN.Management;

[GeneratedComClass]
internal partial class WuwaGameManager : GameManagerBase
{
    internal WuwaGameManager(string gameExecutableNameByPreset,
        string apiResponseUrl,
        string apiResponseAssetUrl,
        string authenticationHash,
        string gameTag,
        string hash1)
    {
        CurrentGameExecutableByPreset = gameExecutableNameByPreset;
        ApiResponseUrl = apiResponseUrl;
        ApiResponseAssetUrl = apiResponseAssetUrl;
        AuthenticationHash = authenticationHash;
        GameTag = gameTag;
        Hash1 = hash1;
    }

    protected override HttpClient? ApiResponseHttpClient
    {
        get => field ??= WuwaUtils.CreateApiHttpClient(ApiResponseAssetUrl, GameTag, AuthenticationHash, "", Hash1);
        set;
    }

    [field: AllowNull]
    [field: MaybeNull]
    private HttpClient ApiDownloadHttpClient
    {
        get => field ??= WuwaUtils.CreateApiHttpClient(ApiResponseAssetUrl, GameTag, AuthenticationHash, "", Hash1);
        set;
    }

    internal string ApiResponseUrl { get; }
    internal string ApiResponseAssetUrl { get; }
    private string GameTag { get; }
    private string AuthenticationHash { get; }
    private string Hash1 { get; }

    private WuwaApiResponseGameConfig? ApiGameConfigResponse { get; set; }
    private string CurrentGameExecutableByPreset { get; }

    private JsonObject CurrentGameConfigNode { get; set; } = new();

    internal string? GameResourceBaseUrl { get; set; }
    internal string? GameResourceBasisPath { get; set; }
    private bool IsInitialized { get; set; }

    protected override GameVersion CurrentGameVersion
    {
        get
        {
            var version = CurrentGameConfigNode.GetConfigValue<string?>("version");
            if (version == null) return GameVersion.Empty;

            if (!GameVersion.TryParse(version, null, out var currentGameVersion))
                currentGameVersion = GameVersion.Empty;

            return currentGameVersion;
        }
        set => CurrentGameConfigNode.SetConfigValue("version", value.ToString());
    }

    protected override GameVersion ApiGameVersion
    {
        get
        {
            if (ApiGameConfigResponse == null) return GameVersion.Empty;
            field = ApiGameConfigResponse.Default?.ConfigReference?.CurrentVersion ?? GameVersion.Empty;
            return field;
        }
        set;
    }

    protected override bool HasPreload => false;
    protected override bool HasUpdate => false;

    protected override bool IsInstalled
    {
        get
        {
            if (string.IsNullOrEmpty(CurrentGameInstallPath))
                return false;
            return IsStandaloneInstall || IsSteamInstall || IsEpicInstall;
        }
    }

    // 修复后的 IsStandaloneInstall，兼容国服/国际服路径
    protected bool IsStandaloneInstall
    {
        get
        {
            if (string.IsNullOrEmpty(CurrentGameInstallPath)) return false;

            // 1. 核心 DLL
            var executablePath1 = Path.Combine(CurrentGameInstallPath,
                "Client\\Binaries\\Win64\\Client-Win64-ShippingBase.dll");

            // 2. 核心 EXE
            var executablePath2 = Path.Combine(CurrentGameInstallPath,
                "Client\\Binaries\\Win64\\Client-Win64-Shipping.exe");

            // 3. SDK 检查 (同时支持 Global 和 Mainland)
            var sdkGlobalPath = Path.Combine(CurrentGameInstallPath,
                "Client\\Binaries\\Win64\\ThirdParty\\KrPcSdk_Global\\KRSDKRes\\KRSDK.bin");

            var sdkCnPath = Path.Combine(CurrentGameInstallPath,
                "Client\\Binaries\\Win64\\ThirdParty\\KrPcSdk_Mainland\\KRSDKRes\\KRSDKConfig.json");

            var isSdkExists = File.Exists(sdkGlobalPath) || File.Exists(sdkCnPath);

            // 4. 配置文件
            var executablePath4 = Path.Combine(CurrentGameInstallPath, "app-game-config.json");

            return File.Exists(executablePath1) &&
                   File.Exists(executablePath2) &&
                   isSdkExists &&
                   File.Exists(executablePath4);
        }
    }

    protected bool IsSteamInstall
    {
        get
        {
            var executablePath1 = Path.Combine(CurrentGameInstallPath ?? string.Empty,
                "Client\\Binaries\\Win64\\ThirdParty\\KrPcSdk_Global\\installscript.vdf");
            var executablePath2 = Path.Combine(CurrentGameInstallPath ?? string.Empty,
                "\\Client\\Binaries\\Win64\\AntiCheatExpert\\SGuard\\x64\\SGuard64.exe");
            var executablePath3 = Path.Combine(CurrentGameInstallPath ?? string.Empty,
                "\\Client\\Binaries\\Win64\\ThirdParty\\KrPcSdk_Global\\KRSDK.dll");

            return File.Exists(executablePath1) &&
                   File.Exists(executablePath2) &&
                   File.Exists(executablePath3);
        }
    }

    protected bool IsEpicInstall
    {
        get
        {
            var executablePath1 = Path.Combine(CurrentGameInstallPath ?? string.Empty,
                "Client\\Binaries\\Win64\\ThirdParty\\KrPcSdk_Global\\EOSSDK-Win64-Shipping.dll");
            return File.Exists(executablePath1);
        }
    }

    public override void Dispose()
    {
        if (IsDisposed) return;
        using (ThisInstanceLock.EnterScope())
        {
            ApiDownloadHttpClient?.Dispose();
            ApiGameConfigResponse = null;
            base.Dispose();
        }
    }

    protected override void SetCurrentGameVersionInner(in GameVersion gameVersion)
    {
        CurrentGameVersion = gameVersion;
    }

    protected override void SetGamePathInner(string gamePath)
    {
        CurrentGameInstallPath = gamePath;
    }

    protected override Task<int> InitAsync(CancellationToken token)
    {
        return InitAsyncInner(true, token);
    }

    internal async Task<int> InitAsyncInner(bool forceInit = false, CancellationToken token = default)
    {
        if (!forceInit && IsInitialized)
            return 0;

        string finalGameTag;
        string finalAuthHash;

        // 国服/国际服参数判断
        if (ApiResponseUrl.Contains("prod-cn") || ApiResponseUrl.Contains("kurogame.xyz"))
        {
            // 国服：直接使用明文
            finalGameTag = "G152";
            finalAuthHash = "10003_Y8xXrXk65DqFHEDgApn3cpK5lfczpFx5";
        }
        else
        {
            // 国际服：使用解密
            finalGameTag = GameTag.AeonPlsHelpMe();
            finalAuthHash = AuthenticationHash.AeonPlsHelpMe();
        }

        // 构建标准 URL
        var gameConfigUrl = $"{ApiResponseUrl}launcher/game/{finalGameTag}/{finalAuthHash}/index.json";

        SharedStatic.InstanceLogger.LogDebug($"[WuwaGameManager] Requesting Game Config: {gameConfigUrl}");

        using var configMessage =
            await ApiResponseHttpClient!.GetAsync(gameConfigUrl, HttpCompletionOption.ResponseHeadersRead, token);
        configMessage.EnsureSuccessStatusCode();

        var jsonResponse = await configMessage.Content.ReadAsStringAsync(token);

        var tmp = JsonSerializer.Deserialize<WuwaApiResponseGameConfig>(jsonResponse,
            WuwaApiResponseContext.Default.WuwaApiResponseGameConfig);
        ApiGameConfigResponse = tmp ?? throw new JsonException("Failed to deserialize API game config response.");

        if (ApiGameConfigResponse.Default?.ConfigReference == null)
            throw new NullReferenceException("ApiGameConfigResponse.ResponseData is null");

        if (ApiGameConfigResponse.Default.ConfigReference.CurrentVersion == GameVersion.Empty)
            throw new NullReferenceException("Game API Launcher cannot retrieve CurrentVersion value!");

        GameResourceBaseUrl = $"{ApiResponseAssetUrl}{ApiGameConfigResponse.Default.ConfigReference.IndexFile}";
        GameResourceBasisPath = ApiGameConfigResponse.Default.ConfigReference.BaseUrl;

        if (GameResourceBasisPath == null)
            throw new NullReferenceException("Game API Launcher cannot retrieve BaseUrl reference value!");

        ApiGameVersion = new GameVersion(ApiGameConfigResponse.Default.ConfigReference.CurrentVersion.ToString());
        IsInitialized = true;

        // -----------------------------------------------------------
        // 【新增】打印版本日志到控制台/Log文件
        // -----------------------------------------------------------
        SharedStatic.InstanceLogger.LogInformation("[WuwaGameManager] =========================================");
        SharedStatic.InstanceLogger.LogInformation(
            $"[WuwaGameManager] Game Region      : {(ApiResponseUrl.Contains("prod-cn") ? "CN (Mainland)" : "Global")}");
        SharedStatic.InstanceLogger.LogInformation($"[WuwaGameManager] Install Path     : {CurrentGameInstallPath}");
        SharedStatic.InstanceLogger.LogInformation($"[WuwaGameManager] Local Version    : {CurrentGameVersion}");
        SharedStatic.InstanceLogger.LogInformation($"[WuwaGameManager] API Latest Ver   : {ApiGameVersion}");
        SharedStatic.InstanceLogger.LogInformation("[WuwaGameManager] =========================================");

        return 0;
    }

    protected override Task DownloadAssetAsyncInner(HttpClient? client, string fileUrl, Stream outputStream,
        PluginDisposableMemory<byte> fileChecksum, PluginFiles.FileReadProgressDelegate? downloadProgress,
        CancellationToken token)
    {
        return base.DownloadAssetAsyncInner(ApiDownloadHttpClient, fileUrl, outputStream, fileChecksum,
            downloadProgress, token);
    }

    protected override Task<string?> FindExistingInstallPathAsyncInner(CancellationToken token)
    {
        return Task.Run(() =>
        {
            if (string.IsNullOrEmpty(CurrentGameInstallPath)) return null;
            var rootSearchPath = Path.GetDirectoryName(Path.GetDirectoryName(CurrentGameInstallPath));
            if (string.IsNullOrEmpty(rootSearchPath)) return null;

            var gameName = Path.GetFileNameWithoutExtension(CurrentGameExecutableByPreset);
            var options = new EnumerationOptions
                { RecurseSubdirectories = true, IgnoreInaccessible = true, MatchType = MatchType.Simple };

            try
            {
                foreach (var filePath in Directory.EnumerateFiles(rootSearchPath, $"*{gameName}*", options))
                {
                    if (token.IsCancellationRequested) return null;
                    var parentPath = Path.GetDirectoryName(filePath);
                    if (parentPath == null) continue;
                    if (File.Exists(Path.Combine(parentPath, "app-game-config.json"))) return parentPath;
                }
            }
            catch
            {
                return null;
            }

            return null;
        }, token);
    }

    public override void LoadConfig()
    {
        if (string.IsNullOrEmpty(CurrentGameInstallPath))
        {
            SharedStatic.InstanceLogger.LogWarning("[WuwaGameManager::LoadConfig] Game directory isn't set!");
            return;
        }

        var filePath = Path.Combine(CurrentGameInstallPath, "app-game-config.json");
        FileInfo fileInfo = new(filePath);

        if (fileInfo.Exists)
            try
            {
                using var fileStream = fileInfo.OpenRead();
#if USELIGHTWEIGHTJSONPARSER
                CurrentGameConfig = WuwaGameLauncherConfig.ParseFrom(fileStream);
#else
                CurrentGameConfigNode = JsonNode.Parse(fileStream) as JsonObject ?? new JsonObject();
#endif
                SharedStatic.InstanceLogger.LogDebug(
                    $"[WuwaGameManager::LoadConfig] Loaded config. Version: {CurrentGameVersion}");
                return;
            }
            catch (Exception ex)
            {
                SharedStatic.InstanceLogger.LogError($"[WuwaGameManager::LoadConfig] Failed to load config: {ex}");
            }

        // 尝试自动恢复
        try
        {
            var exePath = Path.Combine(CurrentGameInstallPath, CurrentGameExecutableByPreset);
            if (File.Exists(exePath))
            {
                SharedStatic.InstanceLogger.LogInformation(
                    "[WuwaGameManager::LoadConfig] Found executable, attempting to initialize and save default config.");
                try
                {
                    InitAsyncInner(true, CancellationToken.None).GetAwaiter().GetResult();
                }
                catch
                {
                }

                // 如果版本未知，尝试设置为 API 版本，防止启动检查失败
                if (CurrentGameVersion == GameVersion.Empty && ApiGameVersion != GameVersion.Empty)
                    CurrentGameVersion = ApiGameVersion;

                try
                {
                    SaveConfig();
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
    }

    public override void SaveConfig()
    {
        if (string.IsNullOrEmpty(CurrentGameInstallPath)) return;

#if !USELIGHTWEIGHTJSONPARSER
        CurrentGameConfigNode.SetConfigValueIfEmpty("version", CurrentGameVersion.ToString());
        CurrentGameConfigNode.SetConfigValueIfEmpty("name",
            ApiGameConfigResponse?.KeyFileCheckList?[2] ??
            Path.GetFileNameWithoutExtension(CurrentGameExecutableByPreset));
#endif
        var installType = GetInstallType();
        CurrentGameConfigNode["InstallType"] = installType;
#if !USELIGHTWEIGHTJSONPARSER
        CurrentGameConfigNode.SetConfigValueIfEmpty("installType", installType);
#endif

        // 如果当前版本为空，强制设为 API 版本
        if (CurrentGameVersion == GameVersion.Empty)
        {
            SharedStatic.InstanceLogger.LogWarning(
                $"[WuwaGameManager::SaveConfig] CurrentVersion is empty. Fallback to API version: {ApiGameVersion}");
            CurrentGameVersion = ApiGameVersion;
        }

        try
        {
            var configPath = Path.Combine(CurrentGameInstallPath, "app-game-config.json");
            Directory.CreateDirectory(CurrentGameInstallPath);
            var writerOptions = new JsonWriterOptions
                { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

#if !USELIGHTWEIGHTJSONPARSER
            using var fs = new FileStream(configPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new Utf8JsonWriter(fs, writerOptions);
            CurrentGameConfigNode.WriteTo(writer);
            writer.Flush();
#endif
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogWarning($"[WuwaGameManager::SaveConfig] Failed: {ex}");
        }
    }

    internal string GetInstallType()
    {
        try
        {
            if (IsStandaloneInstall) return "standalone";
            if (IsSteamInstall) return "steam";
            if (IsEpicInstall) return "epic";
        }
        catch
        {
        }

        return "unknown";
    }
}