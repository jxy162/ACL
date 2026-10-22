using System.Text.Json.Serialization;

namespace ACL.Core.Domain;

/// <summary>OptiFine 引用：版本名与是否以 Mod 形式安装。</summary>
/// <param name="Version">OptiFine 版本名，如 <c>C8</c>、<c>C9_pre10</c>。</param>
/// <param name="AsMod">是否以 Mod 形式安装。</param>
public sealed record OptiFineRef(string Version, bool AsMod = false);

/// <summary>
/// 一个实例的组件集合（加载器 + OptiFine + 相关附加组件）。
/// 采用值语义，<see cref="Equals(ComponentSet?)"/> 与 <see cref="EquivalentTo"/> 均按组件内容判定。
/// </summary>
public sealed class ComponentSet : IEquatable<ComponentSet>
{
    /// <summary>空组件集合（原版）。</summary>
    public static ComponentSet Vanilla { get; } = new(new Dictionary<ModLoaderKind, string>());

    /// <summary>构造组件集合。</summary>
    public ComponentSet(
        IReadOnlyDictionary<ModLoaderKind, string> loaders,
        OptiFineRef? optiFine = null,
        string? fabricApiVersion = null,
        string? optiFabricVersion = null,
        string? quiltVersion = null)
    {
        ArgumentNullException.ThrowIfNull(loaders);
        Loaders = new Dictionary<ModLoaderKind, string>(loaders);
        OptiFine = optiFine;
        FabricApiVersion = fabricApiVersion;
        OptiFabricVersion = optiFabricVersion;
        QuiltVersion = quiltVersion;
    }

    /// <summary>加载器 → 版本。不含 <see cref="ModLoaderKind.OptiFine"/>。</summary>
    public IReadOnlyDictionary<ModLoaderKind, string> Loaders { get; }

    /// <summary>OptiFine 引用；未安装为 <see langword="null"/>。</summary>
    public OptiFineRef? OptiFine { get; }

    /// <summary>Fabric API 版本；未安装为 <see langword="null"/>。</summary>
    public string? FabricApiVersion { get; }

    /// <summary>OptiFabric 版本；未安装为 <see langword="null"/>。</summary>
    public string? OptiFabricVersion { get; }

    /// <summary>Quilt 版本；未安装为 <see langword="null"/>。</summary>
    public string? QuiltVersion { get; }

    /// <summary>是否原版（无任何加载器与 OptiFine）。</summary>
    [JsonIgnore]
    public bool IsVanilla => Loaders.Count == 0 && OptiFine is null;

    /// <summary>按稳定序数排序的加载器种类。</summary>
    [JsonIgnore]
    public IEnumerable<ModLoaderKind> OrderedLoaderKinds
        => Loaders.Keys.OrderBy(ModLoaderKinds.SortKey);

    /// <summary>返回替换了某个加载器版本的新集合。</summary>
    public ComponentSet WithLoader(ModLoaderKind kind, string version)
    {
        ArgumentNullException.ThrowIfNull(version);
        Dictionary<ModLoaderKind, string> map = new(Loaders) { [kind] = version };
        return new ComponentSet(map, OptiFine, FabricApiVersion, OptiFabricVersion, QuiltVersion);
    }

    /// <summary>按组件内容判定等价性。</summary>
    public bool EquivalentTo(ComponentSet? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (Loaders.Count != other.Loaders.Count)
        {
            return false;
        }

        foreach ((ModLoaderKind kind, string version) in Loaders)
        {
            if (!other.Loaders.TryGetValue(kind, out string? otherVersion)
                || !string.Equals(version, otherVersion, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return OptiFine == other.OptiFine
            && string.Equals(FabricApiVersion, other.FabricApiVersion, StringComparison.Ordinal)
            && string.Equals(OptiFabricVersion, other.OptiFabricVersion, StringComparison.Ordinal)
            && string.Equals(QuiltVersion, other.QuiltVersion, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public bool Equals(ComponentSet? other) => EquivalentTo(other);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ComponentSet other && EquivalentTo(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = default;
        foreach (ModLoaderKind kind in OrderedLoaderKinds)
        {
            hash.Add(kind);
            hash.Add(Loaders[kind], StringComparer.Ordinal);
        }

        hash.Add(OptiFine);
        hash.Add(FabricApiVersion, StringComparer.Ordinal);
        hash.Add(OptiFabricVersion, StringComparer.Ordinal);
        hash.Add(QuiltVersion, StringComparer.Ordinal);
        return hash.ToHashCode();
    }
}