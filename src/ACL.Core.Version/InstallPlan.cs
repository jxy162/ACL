using ACL.Core.Domain;

namespace ACL.Core.Version;

/// <summary>安装 / 补全的目标目录。</summary>
/// <param name="GameDir">游戏根目录。</param>
/// <param name="LibrariesDir">库目录。</param>
/// <param name="AssetsDir">资源目录。</param>
/// <param name="VersionsDir">版本目录。</param>
public sealed record InstallTargets(string GameDir, string LibrariesDir, string AssetsDir, string VersionsDir)
{
    /// <summary>按游戏根目录推导各标准子目录。</summary>
    public static InstallTargets ForGameDir(string gameDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDir);
        return new InstallTargets(
            gameDir,
            Path.Combine(gameDir, "libraries"),
            Path.Combine(gameDir, "assets"),
            Path.Combine(gameDir, "versions"));
    }
}

/// <summary>dry-run 安装计划。</summary>
/// <param name="VersionId">版本名。</param>
/// <param name="Downloads">需要下载的项。</param>
/// <param name="Warnings">警告（如缺失信息）。</param>
public sealed record InstallPlan(string VersionId, IReadOnlyList<DownloadItem> Downloads, IReadOnlyList<string> Warnings);