using ACL.Core.Abstractions;

namespace ACL.Infrastructure;

/// <summary>基于 <see cref="System.IO"/> 的物理文件系统实现。</summary>
public sealed class PhysicalFileSystem : IFileSystem
{
    /// <inheritdoc />
    public bool FileExists(string path) => File.Exists(path);

    /// <inheritdoc />
    public bool DirectoryExists(string path) => Directory.Exists(path);

    /// <inheritdoc />
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    /// <inheritdoc />
    public long GetFileLength(string path) => new FileInfo(path).Length;

    /// <inheritdoc />
    public DateTimeOffset GetLastWriteTimeUtc(string path) => File.GetLastWriteTimeUtc(path);

    /// <inheritdoc />
    public Stream OpenRead(string path) => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

    /// <inheritdoc />
    public Stream OpenWrite(string path, bool append = false)
        => new FileStream(path, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None);

    /// <inheritdoc />
    public void Delete(string path) => File.Delete(path);

    /// <inheritdoc />
    public void Move(string sourcePath, string destinationPath, bool overwrite = true)
        => File.Move(sourcePath, destinationPath, overwrite);

    /// <inheritdoc />
    public void Copy(string sourcePath, string destinationPath, bool overwrite = true)
        => File.Copy(sourcePath, destinationPath, overwrite);

    /// <inheritdoc />
    public IEnumerable<string> EnumerateFiles(string path, bool recursive = false)
        => Directory.EnumerateFiles(path, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
}