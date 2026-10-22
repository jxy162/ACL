using ACL.Core.Abstractions;

namespace ACL.Testing;

/// <summary>可编程的 HTTP 传输桩。</summary>
public sealed class StubHttpTransport : IHttpTransport
{
    private readonly Dictionary<string, Func<HttpRequest, Result<HttpResponse>>> _handlers = new(StringComparer.OrdinalIgnoreCase);

    public int RequestCount { get; private set; }

    public StubHttpTransport On(string url, Func<HttpRequest, Result<HttpResponse>> handler)
    {
        _handlers[url] = handler;
        return this;
    }

    public StubHttpTransport OnFail(string url, ErrorCode code = ErrorCode.Network)
        => On(url, _ => Result<HttpResponse>.Failure(code, "stub failure"));

    public StubHttpTransport OnStatus(string url, int statusCode)
        => On(url, _ => StubResponses.Status(statusCode));

    public StubHttpTransport OnText(string url, string text)
        => On(url, _ => StubResponses.Text(text));

    public StubHttpTransport OnContent(string url, byte[] content, bool acceptsRange = true)
        => On(url, request => request.RangeFrom is long from
            ? StubResponses.Partial(content, from)
            : StubResponses.Full(content, acceptsRange));

    public Task<Result<HttpResponse>> SendAsync(HttpRequest request, CancellationToken cancellationToken = default)
    {
        RequestCount++;
        if (_handlers.TryGetValue(request.Uri.ToString(), out Func<HttpRequest, Result<HttpResponse>>? handler))
        {
            return Task.FromResult(handler(request));
        }

        return Task.FromResult(Result<HttpResponse>.Failure(ErrorCode.NotFound, "无处理器"));
    }
}

/// <summary>构造桩响应。</summary>
public static class StubResponses
{
    public static Result<HttpResponse> Full(byte[] content, bool acceptsRange = false)
        => Result<HttpResponse>.Success(new HttpResponse(200, new MemoryStream(content), content.LongLength, acceptsRange));

    public static Result<HttpResponse> Text(string text)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text);
        return Result<HttpResponse>.Success(new HttpResponse(200, new MemoryStream(bytes), bytes.LongLength, false));
    }

    public static Result<HttpResponse> Partial(byte[] full, long from)
    {
        byte[] slice = full[(int)from..];
        return Result<HttpResponse>.Success(new HttpResponse(206, new MemoryStream(slice), slice.LongLength, true));
    }

    public static Result<HttpResponse> Status(int code)
        => Result<HttpResponse>.Success(new HttpResponse(code, null, null, false));
}