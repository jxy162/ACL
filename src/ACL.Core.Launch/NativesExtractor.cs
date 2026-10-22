using System.IO.Compression;
using ACL.Core.Abstractions;

namespace ACL.Core.Launch;

/// <summary>
/// <see cref="INativesExtractor"/> 的默认实现。逐条目解压，跳过目录与排除前缀，
/// 并严格校验目标路径位于 natives 目录内（防路径穿越，见 docs/09）。
/// </summary>
public sealed class NativesExtractor : INativesExtractor
{
    private static readonly string[] DefaultExclusions = ["META-INF/"];

    private readonly IFileSystem _fileSystem;

    /// <summary>构造解压器。</summary>
    public NativesExtractor(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    /// <inheritdoc />
    public Result<int> Extract(
        IReadOnlyList<string> jarPaths,
        string nativesDirectory,
        IReadOnlyList<string>? exclusions = null)
    {
        ArgumentNullException.ThrowIfNull(jarPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativesDirectory);

        string root = Path.GetFullPath(nativesDirectory);
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        IReadOnlyList<string> skip = exclusions is { Count: > 0 } ? exclusions : DefaultExclusions;

        int written = 0;
        _fileSystem.CreateDirectory(root);

        foreach (string jarPath in jarPaths)
        {
            if (string.IsNullOrWhiteSpace(jarPath) || !_fileSystem.FileExists(jarPath))
            {
                continue;
            }

            try
            {
                using Stream archiveStream = _fileSystem.OpenRead(jarPath);
                using ZipArchive archive = new(archiveStream, ZipArchiveMode.Read);

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (!ShouldExtract(entry, skip))
                    {
                        continue;
                    }

                    string destination = Path.GetFullPath(
                        Path.Combine(root, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));

                    if (!IsInside(root, destination, comparison))
                    {
                        // 疑似路径穿越，跳过。
                        continue;
                    }

                    string? directory = Path.GetDirectoryName(destination);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        _fileSystem.CreateDirectory(directory);
                    }

                    using Stream source = entry.Open();
                    using Stream target = _fileSystem.OpenWrite(destination, append: false);
                    source.CopyTo(target);
                    written++;
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                return Result<int>.Failure(ErrorCode.IoError, ex.Message);
            }
        }

        return Result<int>.Success(written);
    }

    private static bool ShouldExtract(ZipArchiveEntry entry, IReadOnlyList<string> exclusions)
    {
        if (entry.Name.Length == 0)
        {
            return false;   // 目录条目
        }

        string fullName = entry.FullName.Replace('\\', '/');
        foreach (string exclusion in exclusions)
        {
            if (fullName.StartsWith(exclusion, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsInside(string root, string candidate, StringComparison comparison)
    {
        if (string.Equals(candidate, root, comparison))
        {
            return true;
        }

        string prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, comparison);
    }
}