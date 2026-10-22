using ACL.Core.Abstractions;

namespace ACL.Core.Download;

/// <summary>
/// BMCLAPI（bangbang93）镜像提供方，实现公开的 URL 重写规则（契约摘要见 docs/05）。
/// 规则可能随上游调整，实现以"尽量接近公开行为"为准。
/// </summary>
public sealed class BmclapiMirrorProvider : IMirrorProvider
{
    /// <summary>镜像主机。</summary>
    public const string MirrorHost = "bmclapi2.bangbang93.com";

    private static readonly Uri Base = new($"https://{MirrorHost}/");

    /// <inheritdoc />
    public string Name => "BMCLAPI";

    /// <inheritdoc />
    public IEnumerable<Uri> Rewrite(MirrorKind kind, Uri origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        string host = origin.Host.ToLowerInvariant();
        string path = origin.AbsolutePath;

        switch (kind)
        {
            case MirrorKind.VersionManifest:
                if (host == "launchermeta.mojang.com" && path.Contains("version_manifest", StringComparison.OrdinalIgnoreCase))
                {
                    yield return new Uri(Base, "mc/game/version_manifest.json");
                }

                break;

            case MirrorKind.VersionMeta:
                if (IsMojangMetaHost(host))
                {
                    yield return Rehost(origin);
                }

                break;

            case MirrorKind.Library:
                if (host is "libraries.minecraft.net" or "piston-data.mojang.com")
                {
                    string relative = path.TrimStart('/');
                    yield return new Uri(Base, "maven/" + relative);
                    yield return new Uri(Base, "libraries/" + relative);
                }

                break;

            case MirrorKind.Asset:
                if (host == "resources.download.minecraft.net")
                {
                    yield return new Uri(Base, "assets/" + path.TrimStart('/'));
                }

                break;

            case MirrorKind.AssetIndex:
                if (IsMojangMetaHost(host))
                {
                    yield return Rehost(origin);
                }

                break;

            case MirrorKind.JavaRuntime:
                if (path.Contains("/v1/products/java-runtime/", StringComparison.OrdinalIgnoreCase)
                    || path.Contains("/v1/products/java-runtime", StringComparison.OrdinalIgnoreCase))
                {
                    yield return Rehost(origin);
                }

                break;

            case MirrorKind.ForgeMeta:
                if (host == "files.minecraftforge.net" && path.Contains("promos", StringComparison.OrdinalIgnoreCase))
                {
                    yield return new Uri(Base, "forge/promos");
                }
                else if (host == "maven.minecraftforge.net")
                {
                    yield return new Uri(Base, "maven" + path);
                }

                break;

            case MirrorKind.NeoForgeMeta:
                if (host == "maven.neoforged.net" && path.Contains("/api/maven/versions/releases/", StringComparison.OrdinalIgnoreCase))
                {
                    string rest = path.Replace("/api/maven/versions/releases/", "/api/maven/details/releases/", StringComparison.OrdinalIgnoreCase);
                    yield return new Uri(Base, "neoforge/meta" + rest);
                }

                break;

            case MirrorKind.FabricMeta:
                if (host == "meta.fabricmc.net")
                {
                    yield return new Uri(Base, "fabric-meta" + path + origin.Query);
                }

                break;

            case MirrorKind.LiteLoaderMeta:
                if (host == "meta.liteloader.com" && path.Contains("liteloader/versions.json", StringComparison.OrdinalIgnoreCase))
                {
                    yield return new Uri(Base, "maven/com/mumfrey/liteloader/versions.json");
                }

                break;

            case MirrorKind.OptiFineMeta:
                if (host == "optifine.net")
                {
                    yield return new Uri(Base, "optifine" + path + origin.Query);
                }

                break;

            default:
                break;
        }
    }

    private static bool IsMojangMetaHost(string host)
        => host is "piston-meta.mojang.com" or "launcher.mojang.com" or "launchermeta.mojang.com" or "piston-data.mojang.com";

    private static Uri Rehost(Uri origin)
        => new UriBuilder(origin) { Scheme = "https", Host = MirrorHost, Port = -1 }.Uri;
}