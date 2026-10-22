using ACL.Core.Abstractions;
using ACL.Core.Version;
using ACL.Testing;
using Xunit;

namespace ACL.Core.Version.Tests;

public class MetadataFetcherTests
{
    [Fact]
    public async Task FetchStringAsync_ReturnsFirstSuccessfulSource()
    {
        StubHttpTransport http = new StubHttpTransport()
            .OnAsync("https://slow.example/x", async (_, ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
                return StubResponses.Text("slow");
            })
            .OnText("https://fast.example/x", "fast");
        MetadataFetcher fetcher = new(http);

        Result<string> result = await fetcher.FetchStringAsync(
            [new Uri("https://slow.example/x"), new Uri("https://fast.example/x")]);

        Assert.True(result.IsSuccess);
        Assert.Equal("fast", result.Value);
    }

    [Fact]
    public async Task FetchStringAsync_SkipsFailedSource()
    {
        StubHttpTransport http = new StubHttpTransport()
            .OnFail("https://bad.example/x")
            .OnStatus("https://missing.example/x", 404)
            .OnText("https://good.example/x", "ok");
        MetadataFetcher fetcher = new(http);

        Result<string> result = await fetcher.FetchStringAsync(
            [new Uri("https://bad.example/x"), new Uri("https://missing.example/x"), new Uri("https://good.example/x")]);

        Assert.True(result.IsSuccess);
        Assert.Equal("ok", result.Value);
    }

    [Fact]
    public async Task FetchStringAsync_NoCandidates_Fails()
    {
        MetadataFetcher fetcher = new(new StubHttpTransport());

        Result<string> result = await fetcher.FetchStringAsync([]);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.NotFound, result.Code);
    }
}