using System.Text;
using ACL.Core.Abstractions;

namespace ACL.Testing;

/// <summary>测试用密钥保护器（可逆混淆，非安全，仅供测试）。</summary>
public sealed class TestSecretProtector : ISecretProtector
{
    private const string Prefix = "TEST:";

    public Result<string> Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        return Result<string>.Success(Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext)));
    }

    public Result<string> Unprotect(string protectedText)
    {
        ArgumentNullException.ThrowIfNull(protectedText);
        if (!protectedText.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return Result<string>.Failure(ErrorCode.Unknown, "缺少保护前缀。");
        }

        return Result<string>.Success(Encoding.UTF8.GetString(Convert.FromBase64String(protectedText[Prefix.Length..])));
    }
}