namespace ACL.Core.Domain;

/// <summary>某个 Minecraft 版本下可选的加载器 / 补丁版本条目。</summary>
/// <param name="Kind">加载器种类。</param>
/// <param name="Version">加载器版本（如 <c>47.2.0</c> / <c>0.15.11</c>）。</param>
/// <param name="GameVersion">对应的 Minecraft 版本。</param>
/// <param name="IsRecommended">是否为推荐版本（稳定 / promos 推荐）。</param>
/// <param name="IsPreview">是否为预览版（如 OptiFine pre）。</param>
/// <param name="DownloadUri">直链（可空；加载器自身需要安装包时提供）。</param>
/// <param name="Note">附加说明（可空）。</param>
public sealed record LoaderOffer(
    ModLoaderKind Kind,
    string Version,
    string GameVersion,
    bool IsRecommended = false,
    bool IsPreview = false,
    Uri? DownloadUri = null,
    string? Note = null)
{
    /// <summary>展示文本。</summary>
    public string Label => $"{ModLoaderNames.Display(Kind)} {Version}";
}

/// <summary>加载器种类的名称映射（自研命名，与第三方启动器无关）。</summary>
public static class ModLoaderNames
{
    /// <summary>中文展示名。</summary>
    public static string Display(ModLoaderKind kind) => kind switch
    {
        ModLoaderKind.Forge => "Forge",
        ModLoaderKind.NeoForge => "NeoForge",
        ModLoaderKind.Fabric => "Fabric",
        ModLoaderKind.Quilt => "Quilt",
        ModLoaderKind.LiteLoader => "LiteLoader",
        ModLoaderKind.OptiFine => "OptiFine",
        _ => "原版",
    };

    /// <summary>用于实例名 / 标识的短名。</summary>
    public static string ShortId(ModLoaderKind kind) => kind switch
    {
        ModLoaderKind.Forge => "forge",
        ModLoaderKind.NeoForge => "neoforge",
        ModLoaderKind.Fabric => "fabric",
        ModLoaderKind.Quilt => "quilt",
        ModLoaderKind.LiteLoader => "liteloader",
        ModLoaderKind.OptiFine => "optifine",
        _ => "vanilla",
    };

    /// <summary>该种类是否需要运行官方安装器（需要本机 Java）。</summary>
    public static bool NeedsInstallerProcess(ModLoaderKind kind)
        => kind is ModLoaderKind.Forge or ModLoaderKind.NeoForge or ModLoaderKind.OptiFine;
}