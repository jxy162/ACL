using System.Security.Cryptography;
using System.Text;
using ACL.Core.Abstractions;

namespace ACL.Infrastructure;

/// <summary>
/// 便携式密钥保护器：
/// Windows 上使用 DPAPI（当前用户）；其他平台回退到 AES-256-GCM，密钥保存在数据目录的 <c>ACL.key</c>。
/// 令牌等敏感信息必须经本组件加密后才落盘。
/// </summary>
public sealed class PortableSecretProtector : ISecretProtector
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly IFileSystem _fileSystem;
    private readonly string _keyPath;
    private readonly object _gate = new();

    /// <summary>构造保护器。</summary>
    /// <param name="fileSystem">文件系统。</param>
    /// <param name="keyPath">回退密钥文件路径（仅非 Windows 平台使用）。</param>
    public PortableSecretProtector(IFileSystem fileSystem, string keyPath)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);
        _keyPath = keyPath;
    }

    /// <inheritdoc />
    public Result<string> Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        if (OperatingSystem.IsWindows())
        {
            try
            {
                byte[] encrypted = ProtectedData.Protect(Utf8NoBom.GetBytes(plaintext), null, DataProtectionScope.CurrentUser);
                return Result<string>.Success("dpapi:" + Convert.ToBase64String(encrypted));
            }
            catch (CryptographicException ex)
            {
                return Result<string>.Failure(ErrorCode.Unknown, ex.Message);
            }
        }

        try
        {
            byte[] nonce = RandomNumberGenerator.GetBytes(12);
            byte[] plain = Utf8NoBom.GetBytes(plaintext);
            byte[] cipher = new byte[plain.Length];
            byte[] tag = new byte[16];
            using AesGcm aes = new(Key(), 16);
            aes.Encrypt(nonce, plain, cipher, tag);
            byte[] payload = [.. nonce, .. tag, .. cipher];
            return Result<string>.Success("aesgcm:" + Convert.ToBase64String(payload));
        }
        catch (IOException ex)
        {
            return Result<string>.Failure(ErrorCode.IoError, ex.Message);
        }
        catch (CryptographicException ex)
        {
            return Result<string>.Failure(ErrorCode.Unknown, ex.Message);
        }
    }

    /// <inheritdoc />
    public Result<string> Unprotect(string protectedText)
    {
        ArgumentNullException.ThrowIfNull(protectedText);

        if (OperatingSystem.IsWindows() && protectedText.StartsWith("dpapi:", StringComparison.Ordinal))
        {
            try
            {
                byte[] plain = ProtectedData.Unprotect(
                    Convert.FromBase64String(protectedText["dpapi:".Length..]), null, DataProtectionScope.CurrentUser);
                return Result<string>.Success(Utf8NoBom.GetString(plain));
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException or PlatformNotSupportedException)
            {
                return Result<string>.Failure(ErrorCode.Unknown, ex.Message);
            }
        }

        if (!protectedText.StartsWith("aesgcm:", StringComparison.Ordinal))
        {
            return Result<string>.Failure(ErrorCode.ConfigInvalid, "未知的密文格式。");
        }

        try
        {
            byte[] payload = Convert.FromBase64String(protectedText["aesgcm:".Length..]);
            if (payload.Length < 28)
            {
                return Result<string>.Failure(ErrorCode.ConfigInvalid, "密文长度不足。");
            }

            byte[] nonce = payload[..12];
            byte[] tag = payload[12..28];
            byte[] cipher = payload[28..];
            byte[] plain = new byte[cipher.Length];
            using AesGcm aes = new(Key(), 16);
            aes.Decrypt(nonce, cipher, tag, plain);
            return Result<string>.Success(Utf8NoBom.GetString(plain));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or IOException)
        {
            return Result<string>.Failure(ErrorCode.Unknown, ex.Message);
        }
    }

    private byte[] Key()
    {
        lock (_gate)
        {
            if (_fileSystem.FileExists(_keyPath))
            {
                using Stream stream = _fileSystem.OpenRead(_keyPath);
                using StreamReader reader = new(stream, Utf8NoBom);
                if (Convert.FromBase64String(reader.ReadToEnd().Trim()) is { Length: 32 } existing)
                {
                    return existing;
                }
            }

            byte[] key = RandomNumberGenerator.GetBytes(32);
            string? directory = Path.GetDirectoryName(_keyPath);
            if (!string.IsNullOrEmpty(directory))
            {
                _fileSystem.CreateDirectory(directory);
            }

            using (Stream stream = _fileSystem.OpenWrite(_keyPath, append: false))
            using (StreamWriter writer = new(stream, Utf8NoBom))
            {
                writer.Write(Convert.ToBase64String(key));
            }

            return key;
        }
    }
}