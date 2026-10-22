using ACL.Core.Abstractions;

namespace ACL.Testing;

/// <summary>可编程的 HTTP 传输桩（支持同步与异步处理器，并记录请求）。</summary>
public sealed class StubHttpTransport : IHttpTransport
{
    private readonly Dictionary<string, Func<HttpRequest, Result<HttpResponse>>> _handlers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Func<HttpRequest, CancellationToken, Task<Result<HttpResponse>>>> _asyncHandlers = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<HttpRequest> _requests = [];

    /// <summary>已收到的请求（按顺序）。</summary>
    public IReadOnlyList<HttpRequest> Requests
    {
        get
        {
            lock (_requests)
            {
                return _requests.ToArray();
            }
        }
    }

    /// <summary>收到的请求总数。</summary>
    public int RequestCount => Requests.Count;

    /// <summary>最近一次请求。</summary>
    public HttpRequest? LastRequest => Requests.Count == 0 ? null : Requests[^1];

    public StubHttpTransport On(string url, Func<HttpRequest, Result<HttpResponse>> handler)
    {
        _handlers[url] = handler;
        return this;
    }

    public StubHttpTransport OnAsync(string url, Func<HttpRequest, CancellationToken, Task<Result<HttpResponse>>> handler)
    {
        _asyncHandlers[url] = handler;
        return this;
    }

    public StubHttpTransport OnFail(string url, ErrorCode code = ErrorCode.Network)
        => On(url, _ => Result<HttpResponse>.Failure(code, "stub failure"));

    public StubHttpTransport OnStatus(string url, int statusCode)
        => On(url, _ => StubResponses.Status(statusCode));

    public StubHttpTransport OnJson(string url, string json, int statusCode = 200)
        => On(url, _ => StubResponses.Json(json, statusCode));

    public StubHttpTransport OnText(string url, string text)
        => On(url, _ => StubResponses.Text(text));

    public StubHttpTransport OnContent(string url, byte[] content, bool acceptsRange = true)
        => On(url, request => request.RangeFrom is long from
            ? StubResponses.Partial(content, from)
            : StubResponses.Full(content, acceptsRange));

    public Task<Result<HttpResponse>> SendAsync(HttpRequest request, CancellationToken cancellationToken = default)
    {
        lock (_requests)
        {
            _requests.Add(request);
        }

        string url = request.Uri.ToString();
        if (_handlers.TryGetValue(url, out Func<HttpRequest, Result<HttpResponse>>? handler))
        {
            return Task.FromResult(handler(request));
        }

        if (_asyncHandlers.TryGetValue(url, out Func<HttpRequest, CancellationToken, Task<Result<HttpResponse>>>? asyncHandler))
        {
            return asyncHandler(request, cancellationToken);
        }

        return Task.FromResult(Result<HttpResponse>.Failure(ErrorCode.NotFound, "无处理器"));
    }
}

/// <summary>构造桩响应。</summary>
public static class StubResponses
{
    public static Result<HttpResponse> Full(byte[] content, bool acceptsRange = false)
        => Result<HttpResponse>.Success(new HttpResponse(200, new MemoryStream(content), content.LongLength, acceptsRange));

    public static Result<HttpResponse> Text(string text, int statusCode = 200)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text);
        return Result<HttpResponse>.Success(new HttpResponse(statusCode, new MemoryStream(bytes), bytes.LongLength, false));
    }

    public static Result<HttpResponse> Json(string json, int statusCode = 200) => Text(json, statusCode);

    public static Result<HttpResponse> Partial(byte[] full, long from)
    {
        byte[] slice = full[(int)from..];
        return Result<HttpResponse>.Success(new HttpResponse(206, new MemoryStream(slice), slice.LongLength, true));
    }

    public static Result<HttpResponse> Status(int code)
        => Result<HttpResponse>.Success(new HttpResponse(code, null, null, false));
}