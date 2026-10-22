using System.Text;
using ACL.Core.Abstractions;

namespace ACL.Core.Version;

/// <summary>
/// 元数据抓取器：对多个候选源**并发取先返回者**（见 ADR-0010），适用于体积较小的元数据。
/// </summary>
public sealed class MetadataFetcher
{
    private readonly IHttpTransport _http;

    /// <summary>构造抓取器。</summary>
    public MetadataFetcher(IHttpTransport http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    /// <summary>并发请求候选源，返回首个成功的正文文本。</summary>
    public async Task<Result<string>> FetchStringAsync(
        IReadOnlyList<Uri> candidates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count == 0)
        {
            return Result<string>.Failure(ErrorCode.NotFound, "无候选源。");
        }

        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        List<Task<Result<string>>> running = [.. candidates.Select(uri => AttemptAsync(uri, linked.Token))];

        ErrorCode lastCode = ErrorCode.NotFound;
        string? lastDetail = null;
        while (running.Count > 0)
        {
            Task<Result<string>> finished = await Task.WhenAny(running).ConfigureAwait(false);
            running.Remove(finished);

            Result<string> result = await finished.ConfigureAwait(false);
            if (result.IsSuccess)
            {
                linked.Cancel();
                await DrainAsync(running).ConfigureAwait(false);
                return result;
            }

            lastCode = result.Code;
            lastDetail = result.Detail;
        }

        return Result<string>.Failure(lastCode, lastDetail);
    }

    private async Task<Result<string>> AttemptAsync(Uri uri, CancellationToken cancellationToken)
    {
        Result<HttpResponse> sent = await _http.SendAsync(new HttpRequest(uri), cancellationToken).ConfigureAwait(false);
        if (!sent.IsSuccess)
        {
            return Result<string>.Failure(sent.Code, sent.Detail);
        }

        using HttpResponse response = sent.Value!;
        if (!response.IsSuccess || response.Content is null)
        {
            return Result<string>.Failure(ErrorCode.NotFound, $"HTTP {response.StatusCode}");
        }

        using StreamReader reader = new(response.Content, Encoding.UTF8);
        string text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        return Result<string>.Success(text);
    }

    private static async Task DrainAsync(List<Task<Result<string>>> tasks)
    {
        foreach (Task<Result<string>> task in tasks)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // 落败者的取消 / 错误在此忽略。
            }
        }
    }
}