using System.Text;
using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Version;

/// <summary>本机已安装的游戏实例（按 <c>versions</c> 目录扫描；不依赖任何启动计划）。</summary>
/// <param name="Id">实例 / 版本名。</param>
/// <param name="Directory">实例目录。</param>
/// <param name="JsonPath">版本 JSON 路径。</param>
/// <param name="HasClientJar">是否存在客户端 jar（可独立启动）。</param>
/// <param name="InheritsFrom">继承的父版本（可空）。</param>
/// <param name="Loader">识别出的加载器。</param>
/// <param name="LoaderVersion">加载器版本（可空）。</param>
/// <param name="ReleasedTime">本地 JSON 的最后写入时间（UTC）。</param>
public sealed record InstalledInstance(
    string Id,
    string Directory,
    string JsonPath,
    bool HasClientJar,
    string? InheritsFrom,
    ModLoaderKind Loader,
    string? LoaderVersion,
    DateTimeOffset ReleasedTime)
{
    /// <summary>加载器展示文本。</summary>
    public string LoaderLabel => Loader == ModLoaderKind.None
        ? "原版"
        : ModLoaderNames.Display(Loader) + (LoaderVersion is null ? string.Empty : " " + LoaderVersion);
}

/// <summary>扫描本机 <c>versions</c> 目录，产出已安装实例列表。</summary>
public sealed class InstalledInstanceScanner
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly IFileSystem _fileSystem;

    /// <summary>构造扫描器。</summary>
    public InstalledInstanceScanner(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    /// <summary>扫描给定 <c>versions</c> 目录。</summary>
    public IReadOnlyList<InstalledInstance> Scan(string versionsDir)
    {
        List<InstalledInstance> result = [];
        if (!_fileSystem.DirectoryExists(versionsDir))
        {
            return result;
        }

        foreach (string directory in DirectChildren(versionsDir))
        {
            string id = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string jsonPath = Path.Combine(directory, id + ".json");
            if (!_fileSystem.FileExists(jsonPath))
            {
                continue;
            }

            string json;
            try
            {
                using Stream stream = _fileSystem.OpenRead(jsonPath);
                using StreamReader reader = new(stream, Utf8NoBom);
                json = reader.ReadToEnd();
            }
            catch (IOException)
            {
                continue;
            }

            string? inherits = null;
            ModLoaderKind loader = ModLoaderKind.None;
            string? loaderVersion = null;
            if (VersionJsonParser.Parse(json) is { IsSuccess: true, Value: { } version })
            {
                inherits = version.InheritsFrom;
                (loader, loaderVersion) = DetectLoader(version);
            }

            if (loader == ModLoaderKind.None)
            {
                (loader, loaderVersion) = DetectFromName(id);
            }

            result.Add(new InstalledInstance(
                id,
                directory,
                jsonPath,
                _fileSystem.FileExists(Path.Combine(directory, id + ".jar")),
                inherits,
                loader,
                loaderVersion,
                SafeLastWrite(jsonPath)));
        }

        return [.. result.OrderByDescending(instance => instance.ReleasedTime)];
    }

    private IReadOnlyList<string> DirectChildren(string parent)
    {
        string normalized = parent.Replace('\\', '/').TrimEnd('/');
        List<string> children = [];
        foreach (string directory in _fileSystem.EnumerateDirectories(parent))
        {
            string candidate = directory.Replace('\\', '/').TrimEnd('/');
            if (candidate.Length <= normalized.Length + 1 || !candidate.StartsWith(normalized + "/", StringComparison.Ordinal))
            {
                continue;
            }

            if (!candidate[(normalized.Length + 1)..].Contains('/'))
            {
                children.Add(directory);
            }
        }

        return children;
    }

    private static (ModLoaderKind Kind, string? Version) DetectLoader(VersionJson version)
    {
        foreach (LibraryRef library in version.Libraries)
        {
            string name = library.Name;
            if (name.StartsWith("net.minecraftforge:forge", StringComparison.OrdinalIgnoreCase))
            {
                return (ModLoaderKind.Forge, Version9(name));
            }

            if (name.StartsWith("net.neoforged", StringComparison.OrdinalIgnoreCase))
            {
                return (ModLoaderKind.NeoForge, Version9(name));
            }

            if (name.StartsWith("net.fabricmc:fabric-loader", StringComparison.OrdinalIgnoreCase))
            {
                return (ModLoaderKind.Fabric, Version9(name));
            }

            if (name.StartsWith("org.quiltmc", StringComparison.OrdinalIgnoreCase))
            {
                return (ModLoaderKind.Quilt, Version9(name));
            }

            if (name.StartsWith("optifine:OptiFine", StringComparison.OrdinalIgnoreCase))
            {
                return (ModLoaderKind.OptiFine, Version9(name));
            }
        }

        return (ModLoaderKind.None, null);
    }

    private static (ModLoaderKind Kind, string? Version) DetectFromName(string id)
    {
        string lower = id.ToLowerInvariant();
        if (lower.Contains("neoforge"))
        {
            return (ModLoaderKind.NeoForge, null);
        }

        if (lower.Contains("forge"))
        {
            return (ModLoaderKind.Forge, null);
        }

        if (lower.Contains("quilt"))
        {
            return (ModLoaderKind.Quilt, null);
        }

        if (lower.Contains("fabric"))
        {
            return (ModLoaderKind.Fabric, null);
        }

        if (lower.Contains("optifine"))
        {
            return (ModLoaderKind.OptiFine, null);
        }

        return (ModLoaderKind.None, null);
    }

    private static string? Version9(string maven)
    {
        string[] parts = maven.Split(':');
        return parts.Length >= 3 ? parts[2] : null;
    }

    private DateTimeOffset SafeLastWrite(string path)
    {
        try
        {
            return _fileSystem.GetLastWriteTimeUtc(path);
        }
        catch (IOException)
        {
            return default;
        }
    }
}