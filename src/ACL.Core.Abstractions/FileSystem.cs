namespace ACL.Core.Abstractions;

/// <summary>文件系统抽象（可注入，便于桩测试）。路径语义遵循宿主平台规则。</summary>
public interface IFileSystem
{
    /// <summary>文件是否存在。</summary>
    bool FileExists(string path);

    /// <summary>目录是否存在。</summary>
    bool DirectoryExists(string path);

    /// <summary>创建目录（含父目录）。</summary>
    void CreateDirectory(string path);

    /// <summary>文件长度（字节）；文件不存在时抛异常。</summary>
    long GetFileLength(string path);

    /// <summary>文件最后写入时间（UTC）。</summary>
    DateTimeOffset GetLastWriteTimeUtc(string path);

    /// <summary>以只读方式打开文件。</summary>
    Stream OpenRead(string path);

    /// <summary>以写入方式打开文件；<paramref name="append"/> 为真时追加。</summary>
    Stream OpenWrite(string path, bool append = false);

    /// <summary>删除文件（不存在则忽略）。</summary>
    void Delete(string path);

    /// <summary>移动文件。</summary>
    void Move(string sourcePath, string destinationPath, bool overwrite = true);

    /// <summary>复制文件。</summary>
    void Copy(string sourcePath, string destinationPath, bool overwrite = true);

    /// <summary>枚举文件。</summary>
    IEnumerable<string> EnumerateFiles(string path, bool recursive = false);
}