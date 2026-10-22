using ACL.Core.Abstractions;

namespace ACL.Core.Download;

/// <summary>
/// 默认镜像策略：汇总各 <see cref="IMirrorProvider"/> 的重写结果，并按偏好给出有序候选。
/// 对资源类（<see cref="MirrorKind.Asset"/> / <see cref="MirrorKind.AssetIndex"/>）强制 HTTPS。
/// </summary>
public sealed class MirrorPolicy : IMirrorPolicy
{
    private readonly IReadOnlyList<IMirrorProvider> _providers;
    private readonly bool _preferOfficialDefault;
    private readonly IReadOnlyDictionary<MirrorKind, bool> _preferOfficialOverrides;

    /// <summary>构造策略。</summary>
    /// <param name="providers">镜像提供方；为 <see langword="null"/> 时使用默认（BMCLAPI）。</param>
    /// <param name="preferOfficial">默认是否优先官方源。</param>
    /// <param name="preferOfficialOverrides">按种类覆盖偏好。</param>
    public MirrorPolicy(
        IEnumerable<IMirrorProvider>? providers = null,
        bool preferOfficial = true,
        IReadOnlyDictionary<MirrorKind, bool>? preferOfficialOverrides = null)
    {
        _providers = providers is null ? [new BmclapiMirrorProvider()] : [.. providers];
        _preferOfficialDefault = preferOfficial;
        _preferOfficialOverrides = preferOfficialOverrides ?? new Dictionary<MirrorKind, bool>();
    }

    /// <inheritdoc />
    public bool PreferOfficial(MirrorKind kind)
        => _preferOfficialOverrides.TryGetValue(kind, out bool value) ? value : _preferOfficialDefault;

    /// <inheritdoc />
    public IReadOnlyList<Uri> Resolve(MirrorKind kind, Uri origin)
    {
        ArgumentNullException.ThrowIfNull(origin);

        Uri normalized = NormalizeOrigin(kind, origin);
        List<Uri> mirrors = [];
        foreach (IMirrorProvider provider in _providers)
        {
            foreach (Uri mirror in provider.Rewrite(kind, normalized))
            {
                if (mirror != normalized && !mirrors.Contains(mirror))
                {
                    mirrors.Add(mirror);
                }
            }
        }

        if (mirrors.Count == 0)
        {
            return [normalized];
        }

        List<Uri> candidates = [];
        if (PreferOfficial(kind))
        {
            candidates.Add(normalized);
            candidates.AddRange(mirrors);
        }
        else
        {
            candidates.AddRange(mirrors);
            candidates.Add(normalized);
        }

        return candidates;
    }

    private static Uri NormalizeOrigin(MirrorKind kind, Uri origin)
    {
        if (kind is MirrorKind.Asset or MirrorKind.AssetIndex
            && origin.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
        {
            return new UriBuilder(origin) { Scheme = "https", Port = -1 }.Uri;
        }

        return origin;
    }
}