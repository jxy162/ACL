using System.Net;
using System.Net.Http.Headers;
using ACL.Core.Abstractions;

namespace ACL.Infrastructure;

/// <summary>基于 <see cref="HttpClient"/> 的 <see cref="IHttpTransport"/> 实现。</summary>
public sealed class HttpTransport : IHttpTransport, IDisposable
{
    private readonly HttpClient _client;
    private readonly bool _ownsClient;

    /// <summary>构造传输。</summary>
    /// <param name="client">可注入的 <see cref="HttpClient"/>；为 <see langword="null"/> 时自建并持有。</param>
    public HttpTransport(HttpClient? client = null)
    {
        _ownsClient = client is null;
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(100) };
    }

    /// <inheritdoc />
    public async Task<Result<HttpResponse>> SendAsync(HttpRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using HttpRequestMessage message = new(
            request.Verb == HttpVerb.Head ? HttpMethod.Head : HttpMethod.Get,
            request.Uri);

        if (request.Headers is not null)
        {
            foreach (KeyValuePair<string, string> header in request.Headers)
            {
                message.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        if (request.RangeFrom is long from)
        {
            message.Headers.Range = new RangeHeaderValue(from, null);
        }

        HttpResponseMessage? response = null;
        try
        {
            response = await _client
                .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            int statusCode = (int)response.StatusCode;
            long? length = response.Content.Headers.ContentLength;
            bool acceptsRange = response.StatusCode == HttpStatusCode.PartialContent
                || response.Headers.AcceptRanges.Contains("bytes");

            if (request.Verb == HttpVerb.Get && response.IsSuccessStatusCode)
            {
                Stream raw = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

                // 正文流接管响应所有权：释放流时一并释放响应。
                HttpResponse owned = new(statusCode, new OwnedHttpStream(raw, response), length, acceptsRange);
                response = null;
                return Result<HttpResponse>.Success(owned);
            }

            response.Dispose();
            response = null;
            return Result<HttpResponse>.Success(new HttpResponse(statusCode, null, length, acceptsRange));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            response?.Dispose();
            throw;
        }
        catch (TaskCanceledException ex)
        {
            response?.Dispose();
            return Result<HttpResponse>.Failure(ErrorCode.Timeout, ex.Message);
        }
        catch (HttpRequestException ex)
        {
            response?.Dispose();
            return Result<HttpResponse>.Failure(ErrorCode.Network, ex.Message);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }
}