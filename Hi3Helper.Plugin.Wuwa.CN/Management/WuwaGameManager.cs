using System;
using System.Collections.Generic;
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
using Microsoft.Win32;

// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypos

namespace Hi3Helper.Plugin.Wuwa.CN.Management;

[GeneratedComClass]
internal partial class WuwaGameManager : GameManagerBase
{
    /// <summary>
    /// Default game directory name, mirroring <c>PluginPresetConfigBase.LauncherGameDirectoryName</c>.
    /// </summary>
    private const string DefaultLauncherGameDirectoryName = "Wuthering Waves Game";

    /// <summary>
    /// Root folder name used by the official Kuro launcher on the drive it is installed to.
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

    internal WuwaGameManager(string gameExecutableNameByPreset,
        string apiResponseUrl,
        string apiResponseAssetUrl,
        string authenticationHash,
        string gameTag,
        string hash1,
        string launcherGameDirectoryName = DefaultLauncherGameDirectoryName)
    {
        CurrentGameExecutableByPreset = gameExecutableNameByPreset;
        ApiResponseUrl = apiResponseUrl;
        ApiResponseAssetUrl = apiResponseAssetUrl;
        AuthenticationHash = authenticationHash;
        GameTag = gameTag;
        Hash1 = hash1;
        LauncherGameDirectoryName = launcherGameDirectoryName;
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
    private string LauncherGameDirectoryName { get; }

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
            foreach (var candidatePath in EnumerateCandidateInstallPaths())
            {
                if (token.IsCancellationRequested) return null;
                if (!IsValidGameInstallPath(candidatePath)) continue;

                SharedStatic.InstanceLogger.LogInformation(
                    "[WuwaGameManager::FindExistingInstallPath] Found existing game installation path: {Path}",
                    candidatePath);
                return candidatePath;
            }

            SharedStatic.InstanceLogger.LogDebug(
                "[WuwaGameManager::FindExistingInstallPath] No existing game installation was found.");
            return null;
        }, token);
    }

    /// <summary>
    /// Enumerates every plausible game installation directory, in order of confidence.
    /// </summary>
    private IEnumerable<string> EnumerateCandidateInstallPaths()
    {
        // 1. Sibling of the currently configured path (keeps the legacy behaviour working).
        if (!string.IsNullOrEmpty(CurrentGameInstallPath))
        {
            if (IsValidGameInstallPath(CurrentGameInstallPath))
                yield return CurrentGameInstallPath;

            var parentPath = Path.GetDirectoryName(CurrentGameInstallPath);
            if (!string.IsNullOrEmpty(parentPath))
                yield return Path.Combine(parentPath, LauncherGameDirectoryName);
        }

        // 2. Installation roots recorded by the official Kuro launcher in the registry.
        foreach (var launcherRootPath in EnumerateKuroLauncherRootPaths())
        {
            yield return Path.Combine(launcherRootPath, LauncherGameDirectoryName);

            // Some setups keep the game right next to the launcher executable.
            yield return launcherRootPath;
        }

        // 3. Well-known default locations on every fixed drive.
        foreach (var candidatePath in EnumerateWellKnownInstallPaths())
            yield return candidatePath;
    }

    /// <summary>
    /// Checks whether the given path really is a Wuthering Waves game directory, by requiring both
    /// the bootstrap executable and the engine executable to be present.
    /// </summary>
    private bool IsValidGameInstallPath(string? candidatePath)
    {
        if (string.IsNullOrEmpty(candidatePath) || !Directory.Exists(candidatePath)) return false;

        var bootstrapExecutablePath = Path.Combine(candidatePath, CurrentGameExecutableByPreset);
        var engineExecutablePath = Path.Combine(candidatePath, EngineExecutableRelativePath);

        return File.Exists(bootstrapExecutablePath) && File.Exists(engineExecutablePath);
    }

    /// <summary>
    /// Reads the launcher installation roots from the registry records written by the official
    /// Kuro launcher. This is what makes the auto-detection work without a manual config.ini.
    /// </summary>
    /// <remarks>
    /// The plugin is only ever loaded by Collapse Launcher, which is a Windows-only application,
    /// hence the unconditional usage of the Windows registry API.
    /// </remarks>
#pragma warning disable CA1416 // Validate platform compatibility
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
            using var rootKey = hive.OpenSubKey(subKeyPath);
            if (rootKey == null) return;

            foreach (var subKeyName in rootKey.GetSubKeyNames())
            {
                using var subKey = rootKey.OpenSubKey(subKeyName);
                if (subKey?.GetValue(KuroLauncherInstallPathValueName) is not string installPath) continue;

                AddCandidatePath(results, installPath);
            }
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogDebug(
                "[WuwaGameManager::FindExistingInstallPath] Failed to read '{SubKeyPath}' from {Hive}: {Error}",
                subKeyPath, hive.Name, ex.Message);
        }
    }

    private static void CollectFromLauncherProtocolRegistration(List<string> results)
    {
        try
        {
            using var classesKey = Registry.LocalMachine.OpenSubKey(@"Software\Classes");
            if (classesKey == null) return;

            foreach (var subKeyName in classesKey.GetSubKeyNames())
            {
                // e.g. "KuroLauncher.10003"
                if (!subKeyName.StartsWith("KuroLauncher.", StringComparison.OrdinalIgnoreCase)) continue;

                using var subKey = classesKey.OpenSubKey(subKeyName);
                using var defaultIconKey = subKey?.OpenSubKey("DefaultIcon");
                if (defaultIconKey?.GetValue(string.Empty) is not string launcherExecutablePath) continue;

                // The value looks like: C:\Program Files\Wuthering Waves\launcher.exe
                var launcherRootPath = Path.GetDirectoryName(launcherExecutablePath.Trim().Trim('"'));
                AddCandidatePath(results, launcherRootPath);
            }
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogDebug(
                "[WuwaGameManager::FindExistingInstallPath] Failed to read the launcher protocol registration: {Error}",
                ex.Message);
        }
    }
#pragma warning restore CA1416 // Validate platform compatibility

    private static void AddCandidatePath(List<string> results, string? candidatePath)
    {
        if (string.IsNullOrWhiteSpace(candidatePath)) return;

        var normalizedPath = candidatePath.Trim().Trim('"').TrimEnd('\\', '/');
        if (normalizedPath.Length == 0) return;
        if (results.Exists(x => string.Equals(x, normalizedPath, StringComparison.OrdinalIgnoreCase))) return;

        results.Add(normalizedPath);
    }

    private IEnumerable<string> EnumerateWellKnownInstallPaths()
    {
        foreach (var driveRootPath in EnumerateFixedDriveRoots())
        {
            yield return Path.Combine(driveRootPath, "Program Files", LauncherRootFolderName, LauncherGameDirectoryName);
            yield return Path.Combine(driveRootPath, "Program Files (x86)", LauncherRootFolderName, LauncherGameDirectoryName);
            yield return Path.Combine(driveRootPath, LauncherRootFolderName, LauncherGameDirectoryName);
            yield return Path.Combine(driveRootPath, "Games", LauncherRootFolderName, LauncherGameDirectoryName);
            yield return Path.Combine(driveRootPath, "Kuro Games", LauncherRootFolderName, LauncherGameDirectoryName);
            yield return Path.Combine(driveRootPath, LauncherGameDirectoryName);
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
                "[WuwaGameManager::FindExistingInstallPath] Failed to enumerate drives: {Error}", ex.Message);
            yield break;
        }

        foreach (var drive in drives)
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