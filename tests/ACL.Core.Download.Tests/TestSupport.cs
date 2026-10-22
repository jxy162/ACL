using ACL.Core.Abstractions;

namespace ACL.Core.Download.Tests;

/// <summary>内存文件系统桩。</summary>
internal sealed class InMemoryFileSystem : IFileSystem
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);
    private readonly HashSet<string> _directories = new(StringComparer.Ordinal);

    public void WriteFile(string path, byte[] content) => _files[Key(path)] = content;

    public byte[] ReadAllBytes(string path) => _files[Key(path)];

    public bool FileExists(string path) => _files.ContainsKey(Key(path));

    public bool DirectoryExists(string path) => _directories.Contains(Key(path));

    public void CreateDirectory(string path) => _directories.Add(Key(path));

    public long GetFileLength(string path) => _files[Key(path)].LongLength;

    public DateTimeOffset GetLastWriteTimeUtc(string path) => DateTimeOffset.UnixEpoch;

    public Stream OpenRead(string path) => new MemoryStream(_files[Key(path)], writable: false);

    public Stream OpenWrite(string path, bool append = false)
    {
        string key = Key(path);
        byte[]? initial = append && _files.TryGetValue(key, out byte[]? existing) ? existing : null;
        return new CommittingStream(initial, bytes => _files[key] = bytes);
    }

    public void Delete(string path) => _files.Remove(Key(path));

    public void Move(string sourcePath, string destinationPath, bool overwrite = true)
    {
        string from = Key(sourcePath);
        string to = Key(destinationPath);
        if (_files.TryGetValue(from, out byte[]? value))
        {
            _files[to] = value;
            _files.Remove(from);
        }
    }

    public void Copy(string sourcePath, string destinationPath, bool overwrite = true)
        => _files[Key(destinationPath)] = _files[Key(sourcePath)];

    public IEnumerable<string> EnumerateFiles(string path, bool recursive = false) => _files.Keys;

    private static string Key(string path) => path.Replace('\\', '/').TrimEnd('/');

    private sealed class CommittingStream : MemoryStream
    {
        private readonly Action<byte[]> _commit;

        public CommittingStream(byte[]? initial, Action<byte[]> commit)
        {
            _commit = commit;
            if (initial is not null)
            {
                Write(initial, 0, initial.Length);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _commit(ToArray());
            }

            base.Dispose(disposing);
        }
    }
}

/// <summary>可编程的 HTTP 传输桩。</summary>
internal sealed class StubHttpTransport : IHttpTransport
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
internal static class StubResponses
{
    public static Result<HttpResponse> Full(byte[] content, bool acceptsRange = false)
        => Result<HttpResponse>.Success(new HttpResponse(200, new MemoryStream(content), content.LongLength, acceptsRange));

    public static Result<HttpResponse> Partial(byte[] full, long from)
    {
        byte[] slice = full[(int)from..];
        return Result<HttpResponse>.Success(new HttpResponse(206, new MemoryStream(slice), slice.LongLength, true));
    }

    public static Result<HttpResponse> Status(int code)
        => Result<HttpResponse>.Success(new HttpResponse(code, null, null, false));
}
/// <summary>测试数据辅助。</summary>
internal static class TestData
{
    public static byte[] MakeBytes(int length, int seed = 0)
    {
        byte[] data = new byte[length];
        for (int i = 0; i < length; i++)
        {
            data[i] = (byte)((i * 31 + seed) % 256);
        }

        return data;
    }

    public static ACL.Core.Domain.HashSpec Sha1Of(byte[] data)
        => ACL.Core.Domain.HashSpec.Sha1(Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(data)).ToLowerInvariant());
}
