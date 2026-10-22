using ACL.Core.Abstractions;
using ACL.Core.Auth;
using ACL.Core.Domain;
using ACL.Core.Java;
using ACL.Core.Launch;
using ACL.Core.Resources;
using ACL.Core.Version;
using ACL.Core.Version.Loaders;

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
            case "search":
                return await SearchAsync(line, cancellationToken).ConfigureAwait(false);
            case "modpack":
                return Modpack(line);
            case "loaders":
                return await LoadersAsync(line, cancellationToken).ConfigureAwait(false);
            case "install":
                return await InstallAsync(line, cancellationToken).ConfigureAwait(false);
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
        _output.WriteLine("  search <关键字> [--source modrinth|curseforge] [--version V] [--loader fabric]");
        _output.WriteLine("  modpack <整合包路径>           解析整合包");
        _output.WriteLine("  loaders <版本> [--kind fabric]  列出可用的加载器版本");
        _output.WriteLine("  install <版本> [--kind fabric] [--loader-version V] [--name N]");
    }

    private static ModLoaderKind ParseKind(string? value) => value?.ToLowerInvariant() switch
    {
        "forge" => ModLoaderKind.Forge,
        "neoforge" => ModLoaderKind.NeoForge,
        "fabric" => ModLoaderKind.Fabric,
        "quilt" => ModLoaderKind.Quilt,
        "optifine" => ModLoaderKind.OptiFine,
        _ => ModLoaderKind.None,
    };

    /// <summary>列出某 Minecraft 版本下可用的加载器版本。</summary>
    private async Task<int> LoadersAsync(CommandLine line, CancellationToken cancellationToken)
    {
        string? game = line.Arg(0);
        if (string.IsNullOrWhiteSpace(game))
        {
            _output.WriteLine("用法：loaders <版本> [--kind forge|neoforge|fabric|quilt|optifine]");
            return 1;
        }

        ModLoaderKind kind = ParseKind(line.Option("kind") ?? "fabric");
        if (kind == ModLoaderKind.None)
        {
            _output.WriteLine("未知加载器（支持 forge / neoforge / fabric / quilt / optifine）。");
            return 1;
        }

        Result<IReadOnlyList<LoaderOffer>> result = await _services.LoaderCatalog
            .GetVersionsAsync(kind, game, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            _output.WriteLine($"获取加载器版本失败：{result.Code} {result.Detail}");
            return 2;
        }

        _output.WriteLine($"{game} 的 {kind} 版本（共 {result.Value!.Count} 个）：");
        foreach (LoaderOffer offer in result.Value!.Take(line.OptionInt("limit", 10)))
        {
            _output.WriteLine($"  {offer.Version,-18} {offer.GameVersion}{(offer.IsRecommended ? "  推荐" : string.Empty)}");
        }

        return 0;
    }

    /// <summary>安装（或补齐）一个版本 / 加载器实例。</summary>
    private async Task<int> InstallAsync(CommandLine line, CancellationToken cancellationToken)
    {
        string? game = line.Arg(0);
        if (string.IsNullOrWhiteSpace(game))
        {
            _output.WriteLine("用法：install <版本> [--kind fabric] [--loader-version V] [--name N] [--java path]");
            return 1;
        }

        ModLoaderKind kind = ParseKind(line.Option("kind") ?? "vanilla");
        string? loaderVersion = line.Option("loader-version");
        string name = line.Option("name") ?? (kind == ModLoaderKind.None ? game : $"{game}-{kind.ToString().ToLowerInvariant()}");

        LoaderInstallRequest request = new(kind, game, loaderVersion, name, _services.GameDirectory, line.Option("java"));
        Result<LoaderInstallResult> result = await _services.LoaderInstaller
            .InstallAsync(request, null, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            _output.WriteLine($"安装失败：{result.Code} {result.Detail}");
            return 2;
        }

        LoaderInstallResult value = result.Value!;
        _output.WriteLine($"已安装实例：{value.InstanceId}");
        _output.WriteLine($"版本 JSON：{value.VersionJsonPath}");
        _output.WriteLine($"下载 / 校验：{value.Downloads.Count} 项");
        foreach (string note in value.Notes)
        {
            _output.WriteLine($"  · {note}");
        }

        return 0;
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

    private async Task<int> SearchAsync(CommandLine line, CancellationToken cancellationToken)
    {
        string? keyword = line.Arg(0);
        if (string.IsNullOrEmpty(keyword))
        {
            _output.WriteLine("用法：search <关键字> [--source modrinth|curseforge] [--version V] [--loader fabric]");
            return 1;
        }

        bool curseForge = string.Equals(line.Option("source"), "curseforge", StringComparison.OrdinalIgnoreCase);
        IResourceIndex index = curseForge ? _services.CurseForge : _services.Modrinth;
        ModLoaderKind? loader = LoaderNames.FromModrinth(line.Option("loader")) is { } kind && kind != ModLoaderKind.None
            ? kind
            : null;

        Result<ResourceSearchResult> result = await index.SearchAsync(
            new ResourceSearchQuery(keyword, ResourceKind.Mod, line.Option("version"), loader)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            _output.WriteLine($"搜索失败：{result.Code} {result.Detail}");
            return 2;
        }

        _output.WriteLine($"[{index.Name}] 共 {result.Value!.Total} 条：");
        foreach (ResourceProject project in result.Value!.Projects.Take(10))
        {
            _output.WriteLine($"  {project.Title,-40} {project.Downloads,10} 下载  {project.Id}");
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