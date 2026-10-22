using System.IO.Compression;
using System.Text;
using ACL.Core.Abstractions;

namespace ACL.Core.Modpack;

/// <summary>基于 zip 的 <see cref="IModpackArchive"/> 实现。</summary>
public sealed class ZipModpackArchive : IModpackArchive
{
    private readonly IFileSystem _fileSystem;
    private readonly string _path;
    private readonly ZipArchive _archive;

    /// <summary>打开归档。</summary>
    public ZipModpackArchive(IFileSystem fileSystem, string path)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!_fileSystem.FileExists(path))
        {
            throw new FileNotFoundException("整合包不存在。", path);
        }

        _path = path;
        Stream stream = _fileSystem.OpenRead(path);
        try
        {
            _archive = new ZipArchive(stream, ZipArchiveMode.Read);
        }
        catch
        {
            stream.Dispose();
            throw;
        }

        EntryNames = [.. _archive.Entries.Where(e => e.Name.Length > 0).Select(e => e.FullName.Replace('\\', '/'))];
    }

    /// <inheritdoc />
    public IReadOnlyList<string> EntryNames { get; }

    /// <inheritdoc />
    public bool Contains(string entryName)
        => _archive.GetEntry(entryName) is not null;

    /// <inheritdoc />
    public string? ReadText(string entryName)
    {
        ZipArchiveEntry? entry = _archive.GetEntry(entryName);
        if (entry is null)
        {
            return null;
        }

        using Stream stream = entry.Open();
        using StreamReader reader = new(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <inheritdoc />
    public Result<int> ExtractTo(string destinationDirectory, string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

        string root = Path.GetFullPath(destinationDirectory);
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        string normalizedPrefix = prefix.Replace('\\', '/').Trim('/');

        int written = 0;
        try
        {
            _fileSystem.CreateDirectory(root);
            foreach (ZipArchiveEntry entry in _archive.Entries)
            {
                if (entry.Name.Length == 0)
                {
                    continue;
                }

                string fullName = entry.FullName.Replace('\\', '/');
                if (normalizedPrefix.Length > 0)
                {
                    if (!fullName.StartsWith(normalizedPrefix + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    fullName = fullName[(normalizedPrefix.Length + 1)..];
                }

                string destination = Path.GetFullPath(Path.Combine(root, fullName.Replace('/', Path.DirectorySeparatorChar)));
                if (!IsInside(root, destination, comparison))
                {
                    continue;   // 防路径穿越
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

        return Result<int>.Success(written);
    }

    /// <inheritdoc />
    public void Dispose() => _archive.Dispose();

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