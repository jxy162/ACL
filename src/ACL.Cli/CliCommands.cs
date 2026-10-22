using ACL.Core.Abstractions;
using ACL.Core.Auth;
using ACL.Core.Domain;
using ACL.Core.Java;
using ACL.Core.Launch;
using ACL.Core.Version;

namespace ACL.Cli;

/// <summary>命令实现（输出到注入的 <see cref="TextWriter"/>，便于测试）。</summary>
public sealed class CliCommands
{
    private readonly CliServices _services;
    private readonly TextWriter _output;

    /// <summary>构造命令集。</summary>
    public CliCommands(CliServices services, TextWriter output)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    /// <summary>执行命令，返回进程退出码。</summary>
    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        CommandLine line = CommandLine.Parse(args);
        switch (line.Command)
        {
            case "versions":
                return await VersionsAsync(line, cancellationToken).ConfigureAwait(false);
            case "java":
                return await JavaAsync(cancellationToken).ConfigureAwait(false);
            case "plan":
                return await PlanAsync(line, cancellationToken).ConfigureAwait(false);
            case "login-offline":
                return LoginOffline(line);
            case "modpack":
                return Modpack(line);
            default:
                Help();
                return line.Command is null ? 0 : 1;
        }
    }

    private void Help()
    {
        _output.WriteLine("ACL 命令行");
        _output.WriteLine("  versions [--limit N]           列出最新版本");
        _output.WriteLine("  java                          列出本机 Java");
        _output.WriteLine("  plan <版本名>                  输出 dry-run 安装计划");
        _output.WriteLine("  login-offline <用户名>         生成离线会话");
        _output.WriteLine("  modpack <整合包路径>           解析整合包");
    }

    private async Task<int> VersionsAsync(CommandLine line, CancellationToken cancellationToken)
    {
        Result<VersionManifest> manifest = await _services.Versions.GetManifestAsync(cancellationToken).ConfigureAwait(false);
        if (!manifest.IsSuccess)
        {
            _output.WriteLine($"获取版本清单失败：{manifest.Code} {manifest.Detail}");
            return 2;
        }

        int limit = line.OptionInt("limit", 10);
        _output.WriteLine($"最新正式版：{manifest.Value!.Latest.Release}");
        _output.WriteLine($"最新快照：{manifest.Value!.Latest.Snapshot}");
        foreach (VersionEntry entry in manifest.Value!.Versions.Take(limit))
        {
            _output.WriteLine($"  {entry.Id,-16} {entry.Type,-10} {entry.ReleaseTime:yyyy-MM-dd}");
        }

        return 0;
    }

    private async Task<int> JavaAsync(CancellationToken cancellationToken)
    {
        System.Collections.Generic.IReadOnlyList<JavaRuntime> runtimes = await _services.JavaLocator
            .LocateAsync(cancellationToken).ConfigureAwait(false);
        if (runtimes.Count == 0)
        {
            _output.WriteLine("未发现可用的 Java。");
            return 1;
        }

        foreach (JavaRuntime runtime in runtimes)
        {
            _output.WriteLine($"  {runtime.Version,-10} {(runtime.Is64Bit ? "x64" : "x86"),-4} {runtime.HomeFolder}");
        }

        return 0;
    }

    private async Task<int> PlanAsync(CommandLine line, CancellationToken cancellationToken)
    {
        string? versionId = line.Arg(0);
        if (string.IsNullOrEmpty(versionId))
        {
            _output.WriteLine("用法：plan <版本名>");
            return 1;
        }

        Result<VersionJson> version = await LoadVersionAsync(versionId, cancellationToken).ConfigureAwait(false);
        if (!version.IsSuccess)
        {
            _output.WriteLine($"读取版本 JSON 失败：{version.Code} {version.Detail}");
            return 2;
        }

        InstallTargets targets = InstallTargets.ForGameDir(_services.GameDirectory);
        InstallPlan plan = new VersionPlanner().Plan(version.Value!, targets, OsSpec.WindowsX64);
        long total = plan.Downloads.Sum(item => item.Size ?? 0);
        _output.WriteLine($"版本 {versionId}：需下载 {plan.Downloads.Count} 项，约 {total / 1024 / 1024} MB");
        foreach (string warning in plan.Warnings)
        {
            _output.WriteLine($"  警告：{warning}");
        }

        return 0;
    }

    private int LoginOffline(CommandLine line)
    {
        string? name = line.Arg(0) ?? line.Option("name");
        if (string.IsNullOrEmpty(name))
        {
            _output.WriteLine("用法：login-offline <用户名>");
            return 1;
        }

        AuthSession session = OfflineIdentity.CreateSession(name);
        _output.WriteLine($"离线会话：{session.Name} uuid={session.Uuid}");
        return 0;
    }

    private int Modpack(CommandLine line)
    {
        string? path = line.Arg(0);
        if (string.IsNullOrEmpty(path))
        {
            _output.WriteLine("用法：modpack <整合包路径>");
            return 1;
        }

        Result<ModpackManifest> manifest = _services.Modpack.Parse(path);
        if (!manifest.IsSuccess)
        {
            _output.WriteLine($"解析整合包失败：{manifest.Code} {manifest.Detail}");
            return 2;
        }

        ModpackManifest value = manifest.Value!;
        _output.WriteLine($"整合包：{value.Name}（{value.Format}）");
        _output.WriteLine($"  Minecraft：{value.MinecraftVersion}");
        foreach (ModpackLoader loader in value.Loaders)
        {
            _output.WriteLine($"  加载器：{loader.Kind} {loader.Version}");
        }

        _output.WriteLine($"  文件：{value.Files.Count}（待解析 {value.UnresolvedFiles.Count}）");
        return 0;
    }

    private async Task<Result<VersionJson>> LoadVersionAsync(string versionId, CancellationToken cancellationToken)
    {
        // 依据清单找到版本条目，再抓取版本 JSON。
        Result<VersionManifest> manifest = await _services.Versions.GetManifestAsync(cancellationToken).ConfigureAwait(false);
        if (!manifest.IsSuccess)
        {
            return Result<VersionJson>.Failure(manifest.Code, manifest.Detail);
        }

        VersionEntry? entry = manifest.Value!.Versions.FirstOrDefault(v => string.Equals(v.Id, versionId, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return Result<VersionJson>.Failure(ErrorCode.NotFound, versionId);
        }

        MetadataFetcher fetcher = new(_services.Http);
        Result<string> body = await fetcher.FetchStringAsync([entry.Url], cancellationToken).ConfigureAwait(false);
        return body.IsSuccess
            ? VersionJsonParser.Parse(body.Value!)
            : Result<VersionJson>.Failure(body.Code, body.Detail);
    }
}