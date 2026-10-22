using System.Security.Cryptography;
using System.Text;
using ACL.Core.Abstractions;

namespace ACL.Platform.Windows;

/// <summary>基于 Windows DPAPI（<see cref="ProtectedData"/>）的密钥保护器。</summary>
public sealed class DpapiSecretProtector : ISecretProtector
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly DataProtectionScope _scope;

    /// <summary>构造保护器。</summary>
    /// <param name="scope">保护范围（默认当前用户）。</param>
    public DpapiSecretProtector(DataProtectionScope scope = DataProtectionScope.CurrentUser)
    {
        _scope = scope;
    }

    /// <inheritdoc />
    public Result<string> Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        try
        {
            byte[] protectedBytes = ProtectedData.Protect(
                Utf8NoBom.GetBytes(plaintext),
                optionalEntropy: null,
                _scope);
            return Result<string>.Success(Convert.ToBase64String(protectedBytes));
        }
        catch (CryptographicException ex)
        {
            return Result<string>.Failure(ErrorCode.Unknown, ex.Message);
        }
        catch (PlatformNotSupportedException ex)
        {
            return Result<string>.Failure(ErrorCode.Unknown, ex.Message);
        }
    }

    /// <inheritdoc />
    public Result<string> Unprotect(string protectedText)
    {
        ArgumentNullException.ThrowIfNull(protectedText);

        try
        {
            byte[] plain = ProtectedData.Unprotect(
                Convert.FromBase64String(protectedText),
                optionalEntropy: null,
                _scope);
            return Result<string>.Success(Utf8NoBom.GetString(plain));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or PlatformNotSupportedException)
        {
            return Result<string>.Failure(ErrorCode.Unknown, ex.Message);
        }
    }
}