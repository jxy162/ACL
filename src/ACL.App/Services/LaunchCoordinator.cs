using ACL.App.ViewModels;
using ACL.Application;
using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Java;
using ACL.Core.Launch;
using ACL.Core.Version;

namespace ACL.App.Services;

/// <summary>
/// 启动协调器：把"选择一个已安装实例 → 直接启动"的整条链路串起来
/// （解析版本链 → 选择 Java → 组装实例设置 → 补齐文件 → 启动监控）。
/// 不再存在"启动计划"这一中间概念，界面只关心实例与结果。
/// </summary>
public sealed class LaunchCoordinator
{
    private readonly AppServices _services;
    private readonly AppState _state;

    /// <summary>构造协调器。</summary>
    public LaunchCoordinator(AppServices services, AppState state)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    /// <summary>启动（或补全后启动）指定实例。</summary>
    public async Task<Result<GameExitInfo>> LaunchAsync(
        string instanceId,
        IProgressSink? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        VersionSource source = _services.CreateVersionSource();
        progress?.Report("读取版本", 0.02, instanceId);
        Result<VersionJson> version = await source.ResolveAsync(instanceId, cancellationToken).ConfigureAwait(false);
        if (!version.IsSuccess)
        {
            return Result<GameExitInfo>.Failure(version.Code, version.Detail);
        }

        InstanceSettings instance = _state.GetInstanceSettings(instanceId);
        JavaRuntime? java = await ResolveJavaAsync(version.Value!, instance).ConfigureAwait(false);
        if (java is null)
        {
            return Result<GameExitInfo>.Failure(ErrorCode.JavaMissing, "未找到合适的 Java 运行时（可在“设置”或实例设置中指定）。");
        }

        AuthSession session = _state.CurrentSession ?? OfflineIdentity.CreateSession(_state.PlayerName);
        InstallTargets targets = InstallTargets.ForGameDir(_services.GameDirectory);
        LaunchSettings settings = new()
        {
            GameDirectory = string.IsNullOrWhiteSpace(instance.WorkingDir) ? _services.GameDirectory : instance.WorkingDir!,
            AssetsRoot = targets.AssetsDir,
            AssetIndexId = version.Value!.Assets ?? "legacy",
            NativesDirectory = Path.Combine(targets.VersionsDir, instanceId, "natives"),
            LibrariesDirectory = targets.LibrariesDir,
            MaxMemoryMb = instance.MaxMemoryMb ?? (int)_state.MaxMemoryMb,
            MinMemoryMb = instance.MinMemoryMb ?? (int)_state.MinMemoryMb,
            ExtraJvmArguments = Split(instance.ExtraJvmArgs),
            ExtraGameArguments = Split(instance.ExtraGameArgs),
            VersionType = version.Value!.Type,
        };

        LaunchOrchestrator orchestrator = new(
            _services.Download,
            _services.Natives,
            _services.LaunchPlanner,
            _services.Monitor,
            _services.FileSystem);

        Result<GameExitInfo> exit = await orchestrator
            .RunAsync(
                new LaunchRequest(instanceId, version.Value!, targets, java, session, settings, CurrentOs()),
                await LoadAssetsAsync(version.Value!, targets, cancellationToken).ConfigureAwait(false),
                progress,
                cancellationToken)
            .ConfigureAwait(false);

        if (exit.IsSuccess)
        {
            GameExitInfo info = exit.Value!;
            _state.AddPlayMinutes(instanceId, info.Duration.TotalMinutes);
            _state.SetLastExit(info);
        }

        return exit;
    }

    /// <summary>解析资源索引（存在时返回，用于一并补齐资源对象）。</summary>
    private async Task<AssetIndex?> LoadAssetsAsync(VersionJson version, InstallTargets targets, CancellationToken cancellationToken)
    {
        if (version.AssetIndex is not { } index)
        {
            return null;
        }

        string path = Path.Combine(targets.AssetsDir, "indexes", index.Id + ".json");
        if (!_services.FileSystem.FileExists(path))
        {
            return null;
        }

        try
        {
            using Stream stream = _services.FileSystem.OpenRead(path);
            using StreamReader reader = new(stream, System.Text.Encoding.UTF8);
            string json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            Result<AssetIndex> parsed = AssetIndexParser.Parse(json, index.Id);
            return parsed.IsSuccess ? parsed.Value : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private async Task<JavaRuntime?> ResolveJavaAsync(VersionJson version, InstanceSettings instance)
    {
        int major = version.JavaVersion?.MajorVersion ?? 8;
        if (!string.IsNullOrWhiteSpace(instance.JavaPath) && _services.FileSystem.FileExists(instance.JavaPath!))
        {
            JavaProbe probe = new(new ACL.Infrastructure.ProcessLauncher(), _services.FileSystem);
            Result<JavaRuntime> probed = await probe.ProbeAsync(instance.JavaPath!, CancellationToken.None).ConfigureAwait(false);
            if (probed.IsSuccess)
            {
                return probed.Value;
            }
        }

        IReadOnlyList<JavaRuntime> runtimes = await _services.JavaLocator.LocateAsync().ConfigureAwait(false);
        JavaRequirement requirement = new(new JavaVersionRange(new Version(major, 0, 0)));
        return new JavaSelector().Select(runtimes, requirement);
    }

    private static IReadOnlyList<string>? Split(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : [.. value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    private static OsSpec CurrentOs()
        => OperatingSystem.IsWindows() ? OsSpec.WindowsX64
            : OperatingSystem.IsMacOS() ? OsSpec.MacOsX64
            : OsSpec.LinuxX64;
}