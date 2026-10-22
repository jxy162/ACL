using System.Text;

namespace ACL.Core.Abstractions;

/// <summary>HTTP 方法。</summary>
public enum HttpVerb
{
    /// <summary>GET。</summary>
    Get = 0,

    /// <summary>HEAD。</summary>
    Head = 1,

    /// <summary>POST。</summary>
    Post = 2,
}

/// <summary>请求体（JSON 或表单）。</summary>
/// <param name="MediaType">媒体类型。</param>
/// <param name="Body">正文。</param>
public sealed record HttpContentSpec(string MediaType, string Body)
{
    /// <summary>JSON 正文。</summary>
    public static HttpContentSpec Json(string body) => new("application/json", body);

    /// <summary>表单正文（<c>application/x-www-form-urlencoded</c>）。</summary>
    public static HttpContentSpec Form(IReadOnlyDictionary<string, string> fields)
        => new(
            "application/x-www-form-urlencoded",
            string.Join("&", fields.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}")));
}

/// <summary>一次 HTTP 请求描述。</summary>
/// <param name="Uri">目标地址。</param>
/// <param name="Verb">方法。</param>
/// <param name="Headers">附加请求头。</param>
/// <param name="RangeFrom">断点续传起始字节（含），<see langword="null"/> 表示整文件。</param>
/// <param name="Content">请求体（仅 POST 使用）。</param>
public sealed record HttpRequest(
    Uri Uri,
    HttpVerb Verb = HttpVerb.Get,
    IReadOnlyDictionary<string, string>? Headers = null,
    long? RangeFrom = null,
    HttpContentSpec? Content = null);

/// <summary>HTTP 响应。持有正文流，调用方负责释放。</summary>
public sealed class HttpResponse : IDisposable
{
    /// <summary>构造响应。</summary>
    /// <param name="statusCode">HTTP 状态码。</param>
    /// <param name="content">正文流（可空）。</param>
    /// <param name="contentLength">声明的内容长度（可空）。</param>
    /// <param name="acceptsRange">服务端是否支持 Range。</param>
    public HttpResponse(int statusCode, Stream? content = null, long? contentLength = null, bool acceptsRange = false)
    {
        StatusCode = statusCode;
        Content = content;
        ContentLength = contentLength;
        AcceptsRange = acceptsRange;
    }

    /// <summary>HTTP 状态码。</summary>
    public int StatusCode { get; }

    /// <summary>正文流（可空）。</summary>
    public Stream? Content { get; }

    /// <summary>声明的内容长度（可空）。</summary>
    public long? ContentLength { get; }

    /// <summary>服务端是否支持 Range 请求。</summary>
    public bool AcceptsRange { get; }

    /// <summary>是否为 2xx。</summary>
    public bool IsSuccess => StatusCode is >= 200 and < 300;

    /// <summary>是否为 206（部分内容，续传命中）。</summary>
    public bool IsPartialContent => StatusCode == 206;

    /// <inheritdoc />
    public void Dispose() => Content?.Dispose();
}

/// <summary><see cref="HttpResponse"/> 辅助方法。</summary>
public static class HttpResponseExtensions
{
    /// <summary>把响应正文读为字符串（空正文返回空串）。</summary>
    public static async Task<string> ReadAsStringAsync(this HttpResponse response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.Content is null)
        {
            return string.Empty;
        }

        using StreamReader reader = new(response.Content, Encoding.UTF8);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// HTTP 传输抽象。传输层错误（DNS/连接/超时）以失败结果返回；
/// 已收到 HTTP 响应（含 4xx/5xx）算 <c>IsSuccess</c> 的成功结果，由调用方检查 <see cref="HttpResponse.StatusCode"/>。
/// </summary>
public interface IHttpTransport
{
    /// <summary>发送请求。</summary>
    Task<Result<HttpResponse>> SendAsync(HttpRequest request, CancellationToken cancellationToken = default);
}