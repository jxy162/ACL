using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Version;

/// <summary>
/// 安装计划生成器（dry-run）：把（已合并的）版本 JSON 转成需要下载的 <see cref="DownloadItem"/> 列表。
/// 覆盖：客户端 jar、资源索引、库产物、按 OS 过滤的 natives。
/// </summary>
public sealed class VersionPlanner
{
    /// <summary>生成计划。</summary>
    public InstallPlan Plan(VersionJson version, InstallTargets targets, OsSpec os)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(os);

        List<DownloadItem> downloads = [];
        List<string> warnings = [];

        AddClient(version, targets, downloads, warnings);
        AddAssetIndex(version, targets, downloads);
        AddLibraries(version, targets, os, downloads, warnings);

        return new InstallPlan(version.Id, downloads, warnings);
    }

    private static void AddClient(VersionJson version, InstallTargets targets, List<DownloadItem> downloads, List<string> warnings)
    {
        DownloadArtifact? client = version.Downloads?.Client;
        if (client?.Url is null)
        {
            warnings.Add("缺少客户端下载信息（可能由父版本提供）。");
            return;
        }

        string path = Path.Combine(targets.VersionsDir, version.Id, version.Id + ".jar");
        downloads.Add(new DownloadItem(client.Url, [], path, ToHash(client.Sha1), client.Size));
    }

    private static void AddAssetIndex(VersionJson version, InstallTargets targets, List<DownloadItem> downloads)
    {
        if (version.AssetIndex is not { } index)
        {
            return;
        }

        string path = Path.Combine(targets.AssetsDir, "indexes", index.Id + ".json");
        downloads.Add(new DownloadItem(index.Url, [], path, ToHash(index.Sha1), index.Size));
    }

    private static void AddLibraries(VersionJson version, InstallTargets targets, OsSpec os, List<DownloadItem> downloads, List<string> warnings)
    {
        foreach (LibraryRef library in version.Libraries)
        {
            if (!RuleEvaluator.IsAllowed(library.Rules, os))
            {
                continue;
            }

            AddLibraryArtifact(library, targets, downloads, warnings);
            AddLibraryNatives(library, targets, os, downloads);
        }
    }

    private static void AddLibraryArtifact(LibraryRef library, InstallTargets targets, List<DownloadItem> downloads, List<string> warnings)
    {
        string? relative = LibraryPaths.ArtifactRelativePath(library);
        if (relative is null)
        {
            warnings.Add($"库缺少可解析的路径：{library.Name}");
            return;
        }

        DownloadArtifact? artifact = library.Downloads?.Artifact;
        Uri? url = artifact?.Url ?? CombineUrl(library.Url, relative);
        if (url is null)
        {
            warnings.Add($"库缺少下载地址：{library.Name}");
            return;
        }

        string path = Path.Combine(targets.LibrariesDir, relative.Replace('/', Path.DirectorySeparatorChar));
        downloads.Add(new DownloadItem(url, [], path, ToHash(artifact?.Sha1), artifact?.Size));
    }

    private static void AddLibraryNatives(LibraryRef library, InstallTargets targets, OsSpec os, List<DownloadItem> downloads)
    {
        string? classifier = LibraryPaths.NativesClassifier(library, os.Name);
        if (classifier is null)
        {
            return;
        }

        if (library.Downloads?.Classifiers is not { } classifiers || !classifiers.TryGetValue(classifier, out DownloadArtifact? native))
        {
            return;
        }

        if (native.Path is null || native.Url is null)
        {
            return;
        }

        string path = Path.Combine(targets.LibrariesDir, native.Path.Replace('/', Path.DirectorySeparatorChar));
        downloads.Add(new DownloadItem(native.Url, [], path, ToHash(native.Sha1), native.Size));
    }

    private static Uri? CombineUrl(string? baseUrl, string relative)
        => string.IsNullOrEmpty(baseUrl) ? null : new Uri(new Uri(EnsureTrailingSlash(baseUrl)), relative);

    private static string EnsureTrailingSlash(string url) => url.EndsWith('/') ? url : url + "/";

    private static HashSpec? ToHash(string? sha1) => string.IsNullOrEmpty(sha1) ? null : HashSpec.Sha1(sha1);
}