using System.Text.Json;
using System.Text.Json.Serialization;

namespace ACL.Core.Abstractions;

/// <summary>统一 JSON 序列化（System.Text.Json，Web 命名约定，忽略 null）。</summary>
public static class JsonSupport
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>序列化为 JSON 文本。</summary>
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    /// <summary>反序列化 JSON 文本。</summary>
    public static Result<T> Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Result<T>.Failure(ErrorCode.ConfigInvalid, "内容为空。");
        }

        try
        {
            T? value = JsonSerializer.Deserialize<T>(json, Options);
            return value is null
                ? Result<T>.Failure(ErrorCode.ConfigInvalid, "反序列化结果为 null。")
                : Result<T>.Success(value);
        }
        catch (JsonException ex)
        {
            return Result<T>.Failure(ErrorCode.ConfigInvalid, ex.Message);
        }
    }
}