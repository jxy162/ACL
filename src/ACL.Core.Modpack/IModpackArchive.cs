using ACL.Core.Abstractions;

namespace ACL.Core.Modpack;

/// <summary>整合包归档读取抽象（zip 等）。</summary>
public interface IModpackArchive : IDisposable
{
    /// <summary>归档中的条目名（相对路径，<c>/</c> 分隔）。</summary>
    IReadOnlyList<string> EntryNames { get; }

    /// <summary>是否包含指定条目。</summary>
    bool Contains(string entryName);

    /// <summary>读取文本条目；不存在返回 <see langword="null"/>。</summary>
    string? ReadText(string entryName);

    /// <summary>把指定前缀下的条目解压到目标目录（去掉前缀）；返回写入的文件数。</summary>
    Result<int> ExtractTo(string destinationDirectory, string prefix);
}