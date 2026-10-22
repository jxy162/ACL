namespace ACL.Core.Domain;

/// <summary>资源对象（以内容哈希寻址）。</summary>
/// <param name="Hash">SHA-1（小写十六进制）。</param>
/// <param name="Size">字节数。</param>
public sealed record AssetObject(string Hash, long Size);

/// <summary>资源索引：逻辑名 → 资源对象。</summary>
/// <param name="Id">索引 ID（如 <c>5</c>）。</param>
/// <param name="Objects">逻辑路径 → 对象。</param>
public sealed record AssetIndex(string Id, IReadOnlyDictionary<string, AssetObject> Objects);