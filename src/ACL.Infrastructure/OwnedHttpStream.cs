namespace ACL.Infrastructure;

/// <summary>把 HTTP 正文流与其所属 <see cref="System.Net.Http.HttpResponseMessage"/> 绑定释放的只读包装流。</summary>
internal sealed class OwnedHttpStream : Stream
{
    private readonly Stream _inner;
    private readonly IDisposable _owner;
    private bool _disposed;

    public OwnedHttpStream(Stream inner, IDisposable owner)
    {
        _inner = inner;
        _owner = owner;
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

    public override int Read(Span<byte> buffer) => _inner.Read(buffer);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => _inner.ReadAsync(buffer, cancellationToken);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _inner.ReadAsync(buffer, offset, count, cancellationToken);

    public override void Flush()
    {
        // 只读流，无操作。
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _inner.Dispose();
            _owner.Dispose();
            _disposed = true;
        }

        base.Dispose(disposing);
    }
}