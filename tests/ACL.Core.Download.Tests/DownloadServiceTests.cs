using System.Security.Cryptography;
using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Testing;
using Xunit;

namespace ACL.Core.Download.Tests;

public class DownloadServiceTests
{
    private const string Target = "D:/mc/versions/1.20.1/client.jar";
    private const string Part = Target + ".part";
    private const string SourceUrl = "https://libraries.minecraft.net/net/minecraft/client.jar";
    private const string MirrorUrl = "https://bmclapi2.bangbang93.com/maven/net/minecraft/client.jar";

    [Fact]
    public async Task DownloadsAndVerifiesHash()
    {
        byte[] content = MakeBytes(3000);
        InMemoryFileSystem fs = new();
        StubHttpTransport http = new StubHttpTransport().OnContent(SourceUrl, content);
        DownloadService service = new(http, fs);
        DownloadItem item = new(new Uri(SourceUrl), [], Target, Sha1Of(content));

        Result<DownloadOutcome> result = await service.DownloadAsync(item);

        Assert.True(result.IsSuccess);
        Assert.Equal(DownloadStatus.Downloaded, result.Value!.Status);
        Assert.Equal(content, fs.ReadAllBytes(Target));
        Assert.False(fs.FileExists(Part));
    }

    [Fact]
    public async Task SkipsWhenTargetAlreadyMatches()
    {
        byte[] content = MakeBytes(2048);
        InMemoryFileSystem fs = new();
        fs.WriteFile(Target, content);
        StubHttpTransport http = new();
        DownloadService service = new(http, fs);
        DownloadItem item = new(new Uri(SourceUrl), [], Target, Sha1Of(content));

        Result<DownloadOutcome> result = await service.DownloadAsync(item);

        Assert.True(result.IsSuccess);
        Assert.Equal(DownloadStatus.Skipped, result.Value!.Status);
        Assert.Equal(0, http.RequestCount);
    }

    [Fact]
    public async Task FallsBackToMirrorWhenPrimaryFails()
    {
        byte[] content = MakeBytes(1500);
        InMemoryFileSystem fs = new();
        StubHttpTransport http = new StubHttpTransport()
            .OnFail(SourceUrl)
            .OnContent(MirrorUrl, content);
        DownloadService service = new(http, fs);
        DownloadItem item = new(new Uri(SourceUrl), [new Uri(MirrorUrl)], Target, Sha1Of(content));

        Result<DownloadOutcome> result = await service.DownloadAsync(item);

        Assert.True(result.IsSuccess);
        Assert.Equal(content, fs.ReadAllBytes(Target));
    }

    [Fact]
    public async Task HashMismatch_FailsAndRemovesPart()
    {
        byte[] served = MakeBytes(1000);
        byte[] expected = MakeBytes(1000, seed: 7);
        InMemoryFileSystem fs = new();
        StubHttpTransport http = new StubHttpTransport().OnContent(SourceUrl, served);
        DownloadService service = new(http, fs);
        DownloadItem item = new(new Uri(SourceUrl), [], Target, Sha1Of(expected));

        Result<DownloadOutcome> result = await service.DownloadAsync(item);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.HashMismatch, result.Code);
        Assert.False(fs.FileExists(Part));
        Assert.False(fs.FileExists(Target));
    }

    [Fact]
    public async Task ResumesPartialDownload_WhenServerSupportsRange()
    {
        byte[] content = MakeBytes(4096);
        InMemoryFileSystem fs = new();
        fs.WriteFile(Part, content[..1024]);
        StubHttpTransport http = new StubHttpTransport().OnContent(SourceUrl, content, acceptsRange: true);
        DownloadService service = new(http, fs);
        DownloadItem item = new(new Uri(SourceUrl), [], Target, Sha1Of(content));

        Result<DownloadOutcome> result = await service.DownloadAsync(item);

        Assert.True(result.IsSuccess);
        Assert.Equal(content, fs.ReadAllBytes(Target));
    }

    [Fact]
    public async Task RestartsFromZero_WhenServerIgnoresRange()
    {
        byte[] content = MakeBytes(4096);
        InMemoryFileSystem fs = new();
        fs.WriteFile(Part, content[..1024]);
        StubHttpTransport http = new StubHttpTransport()
            .On(SourceUrl, _ => StubResponses.Full(content, acceptsRange: false));
        DownloadService service = new(http, fs);
        DownloadItem item = new(new Uri(SourceUrl), [], Target, Sha1Of(content));

        Result<DownloadOutcome> result = await service.DownloadAsync(item);

        Assert.True(result.IsSuccess);
        Assert.Equal(content, fs.ReadAllBytes(Target));
    }

    [Fact]
    public async Task ReportsMonotonicProgress()
    {
        byte[] content = MakeBytes(5000);
        InMemoryFileSystem fs = new();
        StubHttpTransport http = new StubHttpTransport().OnContent(SourceUrl, content);
        DownloadService service = new(http, fs);
        DownloadItem item = new(new Uri(SourceUrl), [], Target, Sha1Of(content));
        RecordingSink sink = new();

        await service.DownloadAsync(item, sink);

        Assert.NotEmpty(sink.Reports);
        Assert.Equal(1d, sink.Reports[^1].Fraction, 3);
    }

    [Fact]
    public async Task DownloadMany_DownloadsAllItems()
    {
        byte[] content = MakeBytes(800);
        InMemoryFileSystem fs = new();
        StubHttpTransport http = new StubHttpTransport().OnContent(SourceUrl, content);
        DownloadService service = new(http, fs);
        DownloadItem[] items =
        [
            new(new Uri(SourceUrl), [], "D:/mc/a.bin", Sha1Of(content)),
            new(new Uri(SourceUrl), [], "D:/mc/b.bin", Sha1Of(content)),
            new(new Uri(SourceUrl), [], "D:/mc/c.bin", Sha1Of(content)),
        ];

        Result<IReadOnlyList<DownloadOutcome>> result = await service.DownloadManyAsync(items, maxConcurrency: 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Count);
        Assert.All(result.Value!, o => Assert.Equal(DownloadStatus.Downloaded, o.Status));
    }

    private static HashSpec Sha1Of(byte[] data) => HashSpec.Sha1(Convert.ToHexString(SHA1.HashData(data)).ToLowerInvariant());

    private static byte[] MakeBytes(int length, int seed = 0)
    {
        byte[] data = new byte[length];
        for (int i = 0; i < length; i++)
        {
            data[i] = (byte)((i * 31 + seed) % 256);
        }

        return data;
    }

    private sealed class RecordingSink : IProgressSink
    {
        private readonly List<ProgressReport> _reports = [];

        public IReadOnlyList<ProgressReport> Reports => _reports;

        public void Report(ProgressReport report) => _reports.Add(report);
    }
}