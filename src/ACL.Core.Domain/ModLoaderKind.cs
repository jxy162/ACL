namespace ACL.Core.Domain;

/// <summary>模组加载器 / 补丁种类。</summary>
/// <remarks>
/// <see cref="ComponentSet.Loaders"/> 以本枚举为键保存"加载器 → 版本"，但 <see cref="OptiFine"/>
/// 因需要额外信息（是否以 Mod 形式安装），由 <see cref="ComponentSet.OptiFine"/> 单独承载。
/// </remarks>
public enum ModLoaderKind
{
    /// <summary>无加载器（原版）。</summary>
    None = 0,

    /// <summary>Forge。</summary>
    Forge,

    /// <summary>NeoForge。</summary>
    NeoForge,

    /// <summary>Fabric。</summary>
    Fabric,

    /// <summary>Quilt。</summary>
    Quilt,

    /// <summary>LiteLoader。</summary>
    LiteLoader,

    /// <summary>OptiFine（通常作为补丁或 Mod）。</summary>
    OptiFine,
}

/// <summary>加载器排序辅助。</summary>
public static class ModLoaderKinds
{
    /// <summary>返回用于稳定排序的加载器序数。</summary>
    public static int SortKey(ModLoaderKind kind) => kind switch
    {
        ModLoaderKind.None => 0,
        ModLoaderKind.Forge => 1,
        ModLoaderKind.NeoForge => 2,
        ModLoaderKind.Fabric => 3,
        ModLoaderKind.Quilt => 4,
        ModLoaderKind.LiteLoader => 5,
        ModLoaderKind.OptiFine => 6,
        _ => int.MaxValue,
    };
}