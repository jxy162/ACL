using ACL.Core.Domain;

namespace ACL.Core.Version;

/// <summary>资源对象下载计划：按内容哈希寻址到 <c>assets/objects/&lt;hh&gt;/&lt;hash&gt;</c>。</summary>
public sealed class AssetPlanner
{
    /// <summary>资源对象基址。</summary>
    public static readonly Uri DefaultBaseUri = new("https://resources.download.minecraft.net/");

    private readonly Uri _baseUri;

    /// <summary>构造资源计划器。</summary>
    /// <param name="baseUri">资源对象基址；为 <see langword="null"/> 时使用官方基址。</param>
    public AssetPlanner(Uri? baseUri = null)
    {
        _baseUri = baseUri ?? DefaultBaseUri;
    }

    /// <summary>生成资源对象下载项。</summary>
    public IReadOnlyList<DownloadItem> Plan(AssetIndex index, InstallTargets targets)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(targets);

        List<DownloadItem> downloads = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (AssetObject asset in index.Objects.Values)
        {
            if (!seen.Add(asset.Hash))
            {
                continue;
            }

            string prefix = asset.Hash[..2];
            Uri url = new(_baseUri, $"{prefix}/{asset.Hash}");
            string path = Path.Combine(targets.AssetsDir, "objects", prefix, asset.Hash);
            downloads.Add(new DownloadItem(url, [], path, HashSpec.Sha1(asset.Hash), asset.Size));
        }

        return downloads;
    }
}