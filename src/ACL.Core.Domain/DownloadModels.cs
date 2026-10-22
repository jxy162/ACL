namespace ACL.Core.Domain;

/// <summary>哈希算法种类。</summary>
public enum HashKind
{
    /// <summary>SHA-1。</summary>
    Sha1,

    /// <summary>SHA-256。</summary>
    Sha256,

    /// <summary>MD5。</summary>
    Md5,
}

/// <summary>哈希校验规格。</summary>
/// <param name="Kind">算法。</param>
/// <param name="Value">小写十六进制摘要。</param>
public sealed record HashSpec(HashKind Kind, string Value)
{
    /// <summary>以 SHA-1 构造。</summary>
    public static HashSpec Sha1(string value) => new(HashKind.Sha1, value);

    /// <summary>以 SHA-256 构造。</summary>
    public static HashSpec Sha256(string value) => new(HashKind.Sha256, value);
}

/// <summary>单个下载项。</summary>
/// <param name="Primary">首选 URL（通常为官方源）。</param>
/// <param name="Mirrors">镜像候选（有序）。</param>
/// <param name="TargetPath">落盘目标路径。</param>
/// <param name="Hash">校验规格（可选）。</param>
/// <param name="Size">期望大小（字节，可选）。</param>
public sealed record DownloadItem(
    Uri Primary,
    IReadOnlyList<Uri> Mirrors,
    string TargetPath,
    HashSpec? Hash = null,
    long? Size = null);