namespace ACL.Core.Domain;

/// <summary>Maven 坐标（<c>group:artifact:version[:classifier][@extension]</c>）与库内相对路径解析。</summary>
public sealed record MavenCoordinates(string Group, string Artifact, string Version, string? Classifier = null, string Extension = "jar")
{
    /// <summary>解析 Maven 坐标字符串。</summary>
    public static MavenCoordinates Parse(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        string[] parts = name.Split(':');
        if (parts.Length < 3)
        {
            throw new FormatException($"无效的 Maven 坐标：{name}");
        }

        string version = parts[2];
        string? classifier = parts.Length > 3 && parts[3].Length > 0 ? parts[3] : null;

        // 扩展名可能附在最后一段（classifier 或 version）上，如 :universal@zip / :1.0@zip
        string extension = "jar";
        string last = classifier ?? version;
        int at = last.IndexOf('@', StringComparison.Ordinal);
        if (at >= 0)
        {
            extension = last[(at + 1)..];
            if (classifier is not null)
            {
                classifier = last[..at];
            }
            else
            {
                version = last[..at];
            }
        }

        return new MavenCoordinates(parts[0], parts[1], version, classifier, extension);
    }

    /// <summary>库内的相对路径，如 <c>net/minecraftforge/forge/1.20.1-47.2.0/forge-1.20.1-47.2.0.jar</c>。</summary>
    public string RelativePath
    {
        get
        {
            string folder = Group.Replace('.', '/');
            string file = Classifier is null
                ? $"{Artifact}-{Version}.{Extension}"
                : $"{Artifact}-{Version}-{Classifier}.{Extension}";
            return $"{folder}/{Artifact}/{Version}/{file}";
        }
    }
}