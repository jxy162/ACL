using System.Security.Cryptography;
using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Download;

/// <summary>基于流式哈希的 <see cref="IFileChecker"/> 默认实现。</summary>
public sealed class FileChecker : IFileChecker
{
    private const int BufferSize = 81920;

    private readonly IFileSystem _fileSystem;

    /// <summary>构造校验器。</summary>
    public FileChecker(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    /// <inheritdoc />
    public async Task<bool> MatchesAsync(string path, HashSpec spec, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);

        if (!_fileSystem.FileExists(path))
        {
            return false;
        }

        HashAlgorithmName algorithm = spec.Kind switch
        {
            HashKind.Sha1 => HashAlgorithmName.SHA1,
            HashKind.Sha256 => HashAlgorithmName.SHA256,
            HashKind.Md5 => HashAlgorithmName.MD5,
            _ => throw new ArgumentOutOfRangeException(nameof(spec), spec.Kind, "未知哈希算法。"),
        };

        using IncrementalHash hasher = IncrementalHash.CreateHash(algorithm);
        using Stream stream = _fileSystem.OpenRead(path);
        byte[] buffer = new byte[BufferSize];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            hasher.AppendData(buffer, 0, read);
        }

        string actual = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
        return string.Equals(actual, spec.Value, StringComparison.OrdinalIgnoreCase);
    }
}