using System.Diagnostics;
using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Testing;
using Xunit;

namespace ACL.Core.Download.Tests;

public class ThrottleTests
{
    private const string Url = "https://libraries.minecraft.net/net/minecraft/big.jar";
    private const string Target = "D:/mc/big.jar";

    [Fact]
    public async Task Throttle_LimitsThroughput()
    {
        byte[] content = TestData.MakeBytes(60_000);
        InMemoryFileSystem fs = new();
        StubHttpTransport http = new StubHttpTransport().OnContent(Url, content);
        DownloadService service = new(http, fs, throttle: new DownloadThrottle(bytesPerSecond: 120_000));
        DownloadItem item = new(new Uri(Url), [], Target, TestData.Sha1Of(content));

        Stopwatch watch = Stopwatch.StartNew();
        Result<DownloadOutcome> result = await service.DownloadAsync(item);
        watch.Stop();

        Assert.True(result.IsSuccess);
        Assert.True(
            watch.Elapsed >= TimeSpan.FromSeconds(0.3),
            $"预期被限速（约 0.5s），实际 {watch.Elapsed.TotalSeconds:F2}s。");
    }

    [Fact]
    public async Task Unlimited_DoesNotDelay()
    {
        byte[] content = TestData.MakeBytes(200_000);
        InMemoryFileSystem fs = new();
        StubHttpTransport http = new StubHttpTransport().OnContent(Url, content);
        DownloadService service = new(http, fs);
        DownloadItem item = new(new Uri(Url), [], Target, TestData.Sha1Of(content));

        Stopwatch watch = Stopwatch.StartNew();
        Result<DownloadOutcome> result = await service.DownloadAsync(item);
        watch.Stop();

        Assert.True(result.IsSuccess);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), $"不限速不应有显著延迟，实际 {watch.Elapsed.TotalSeconds:F2}s。");
    }
}