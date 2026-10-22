using ACL.Core.Abstractions;

namespace ACL.Core.Version;

/// <summary>实例相关 JSON 的序列化 / 反序列化（委托 <see cref="JsonSupport"/>）。</summary>
public static class InstanceJson
{
    /// <summary>序列化为 JSON 文本。</summary>
    public static string Serialize<T>(T value) => JsonSupport.Serialize(value);

    /// <summary>反序列化 JSON 文本。</summary>
    public static Result<T> Deserialize<T>(string json) => JsonSupport.Deserialize<T>(json);
}