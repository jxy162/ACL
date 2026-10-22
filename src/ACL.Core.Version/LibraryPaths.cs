using ACL.Core.Domain;

namespace ACL.Core.Version;

/// <summary>库路径解析：由库引用得到库内相对路径与 natives 分类（供计划生成与启动共用）。</summary>
public static class LibraryPaths
{
    /// <summary>库主产物的库内相对路径；优先 <c>downloads.artifact.path</c>，否则由 Maven 坐标推导。</summary>
    public static string? ArtifactRelativePath(LibraryRef library)
    {
        ArgumentNullException.ThrowIfNull(library);

        string? declared = library.Downloads?.Artifact?.Path;
        if (!string.IsNullOrEmpty(declared))
        {
            return declared;
        }

        try
        {
            return MavenCoordinates.Parse(library.Name).RelativePath;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>给定 OS 下的 natives 分类名；无则 <see langword="null"/>。</summary>
    public static string? NativesClassifier(LibraryRef library, string osName)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentException.ThrowIfNullOrWhiteSpace(osName);

        return library.Natives is not null && library.Natives.TryGetValue(osName, out string? classifier)
            ? classifier
            : null;
    }
}