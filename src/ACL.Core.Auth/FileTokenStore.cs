using System.Text;
using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Auth;

/// <summary>基于文件系统的 <see cref="ITokenStore"/>：会话 JSON 落盘，令牌字段经 <see cref="ISecretProtector"/> 加密。</summary>
public sealed class FileTokenStore : ITokenStore
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly IFileSystem _fileSystem;
    private readonly ISecretProtector _protector;
    private readonly string _directory;

    /// <summary>构造令牌存储。</summary>
    /// <param name="fileSystem">文件系统。</param>
    /// <param name="protector">密钥保护器（如 Windows DPAPI）。</param>
    /// <param name="directory">存储目录。</param>
    public FileTokenStore(IFileSystem fileSystem, ISecretProtector protector, string directory)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    /// <inheritdoc />
    public Result<AuthSession> Load(string accountId)
    {
        string path = PathFor(accountId);
        if (!_fileSystem.FileExists(path))
        {
            return Result<AuthSession>.Failure(ErrorCode.NotFound, path);
        }

        StoredSession? stored;
        try
        {
            using Stream stream = _fileSystem.OpenRead(path);
            using StreamReader reader = new(stream, Utf8NoBom);
            stored = JsonSupport.Deserialize<StoredSession>(reader.ReadToEnd()).Value;
        }
        catch (IOException ex)
        {
            return Result<AuthSession>.Failure(ErrorCode.IoError, ex.Message);
        }

        if (stored is null)
        {
            return Result<AuthSession>.Failure(ErrorCode.ConfigInvalid, "会话文件无效。");
        }

        Result<string> access = _protector.Unprotect(stored.ProtectedAccessToken);
        if (!access.IsSuccess)
        {
            return Result<AuthSession>.Failure(access.Code, access.Detail);
        }

        string? refresh = null;
        if (stored.ProtectedRefreshToken is not null)
        {
            Result<string> unprotected = _protector.Unprotect(stored.ProtectedRefreshToken);
            if (!unprotected.IsSuccess)
            {
                return Result<AuthSession>.Failure(unprotected.Code, unprotected.Detail);
            }

            refresh = unprotected.Value;
        }

        if (!Enum.TryParse(stored.Type, ignoreCase: true, out AccountType type))
        {
            type = AccountType.Offline;
        }

        return Result<AuthSession>.Success(new AuthSession(type, stored.Uuid, stored.Name, access.Value!, refresh, stored.ExpiresAt));
    }

    /// <inheritdoc />
    public Result Save(string accountId, AuthSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        Result<string> access = _protector.Protect(session.AccessToken);
        if (!access.IsSuccess)
        {
            return Result.Failure(access.Code, access.Detail);
        }

        string? protectedRefresh = null;
        if (session.RefreshToken is not null)
        {
            Result<string> refresh = _protector.Protect(session.RefreshToken);
            if (!refresh.IsSuccess)
            {
                return Result.Failure(refresh.Code, refresh.Detail);
            }

            protectedRefresh = refresh.Value;
        }

        StoredSession stored = new(session.Type.ToString(), session.Uuid, session.Name, access.Value!, protectedRefresh, session.ExpiresAt);

        try
        {
            string path = PathFor(accountId);
            _fileSystem.CreateDirectory(_directory);
            using Stream stream = _fileSystem.OpenWrite(path, append: false);
            using StreamWriter writer = new(stream, Utf8NoBom);
            writer.Write(JsonSupport.Serialize(stored));
            return Result.Ok;
        }
        catch (IOException ex)
        {
            return Result.Failure(ErrorCode.IoError, ex.Message);
        }
    }

    /// <inheritdoc />
    public Result Delete(string accountId)
    {
        try
        {
            _fileSystem.Delete(PathFor(accountId));
            return Result.Ok;
        }
        catch (IOException ex)
        {
            return Result.Failure(ErrorCode.IoError, ex.Message);
        }
    }

    private string PathFor(string accountId) => Path.Combine(_directory, Sanitize(accountId) + ".json");

    private static string Sanitize(string accountId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);

        StringBuilder builder = new(accountId.Length);
        foreach (char ch in accountId)
        {
            builder.Append(char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '_');
        }

        string result = builder.ToString().Trim('.');
        return result.Length == 0 ? "account" : result;
    }

    private sealed record StoredSession(
        string Type,
        string Uuid,
        string Name,
        string ProtectedAccessToken,
        string? ProtectedRefreshToken,
        DateTimeOffset? ExpiresAt);
}