using System.Text;
using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Auth;

/// <summary>
/// 本地账号档案存储：只保存"本机登录过的账号"（离线与正版分开记录），
/// 令牌交给 <see cref="ITokenStore"/> 加密落盘。
/// </summary>
public interface IAccountStore
{
    /// <summary>列出本地账号（最近使用在前）。</summary>
    Result<IReadOnlyList<AccountProfile>> List();

    /// <summary>新增 / 更新账号（提供会话时一并保存令牌）。</summary>
    Result Save(AccountProfile profile, AuthSession? session = null);

    /// <summary>删除账号（同时删除其令牌）。</summary>
    Result Delete(string id);

    /// <summary>读取账号令牌（不存在返回 <see cref="ErrorCode.NotFound"/>）。</summary>
    Result<AuthSession> LoadSession(string id);
}

/// <summary>基于文件系统的 <see cref="IAccountStore"/> 实现（便携式 <c>ACL.accounts.json</c>）。</summary>
public sealed class AccountStore : IAccountStore
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly IFileSystem _fileSystem;
    private readonly ITokenStore _tokens;
    private readonly string _path;

    /// <summary>构造账号存储。</summary>
    public AccountStore(IFileSystem fileSystem, ITokenStore tokens, string path)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    /// <inheritdoc />
    public Result<IReadOnlyList<AccountProfile>> List()
    {
        Result<AccountsFile> loaded = Read();
        if (!loaded.IsSuccess)
        {
            return loaded.Code == ErrorCode.NotFound
                ? Result<IReadOnlyList<AccountProfile>>.Success([])
                : Result<IReadOnlyList<AccountProfile>>.Failure(loaded.Code, loaded.Detail);
        }

        IReadOnlyList<AccountProfile> accounts = [.. loaded.Value!.Accounts.OrderByDescending(a => a.LastUsedAt)];
        return Result<IReadOnlyList<AccountProfile>>.Success(accounts);
    }

    /// <inheritdoc />
    public Result Save(AccountProfile profile, AuthSession? session = null)
    {
        ArgumentNullException.ThrowIfNull(profile);

        Result<AccountsFile> loaded = Read();
        List<AccountProfile> accounts = loaded.IsSuccess ? [.. loaded.Value!.Accounts] : [];
        int index = accounts.FindIndex(a => string.Equals(a.Id, profile.Id, StringComparison.Ordinal));
        if (index >= 0)
        {
            accounts[index] = profile;
        }
        else
        {
            accounts.Add(profile);
        }

        if (session is not null)
        {
            Result stored = _tokens.Save(profile.Id, session);
            if (!stored.IsSuccess)
            {
                return stored;
            }
        }

        return Write(new AccountsFile(1, accounts));
    }

    /// <inheritdoc />
    public Result Delete(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        Result<AccountsFile> loaded = Read();
        if (loaded.IsSuccess)
        {
            List<AccountProfile> accounts = [.. loaded.Value!.Accounts.Where(a => !string.Equals(a.Id, id, StringComparison.Ordinal))];
            Result written = Write(new AccountsFile(1, accounts));
            if (!written.IsSuccess)
            {
                return written;
            }
        }

        return _tokens.Delete(id);
    }

    /// <inheritdoc />
    public Result<AuthSession> LoadSession(string id) => _tokens.Load(id);

    private Result<AccountsFile> Read()
    {
        if (!_fileSystem.FileExists(_path))
        {
            return Result<AccountsFile>.Failure(ErrorCode.NotFound, _path);
        }

        try
        {
            using Stream stream = _fileSystem.OpenRead(_path);
            using StreamReader reader = new(stream, Utf8NoBom);
            Result<AccountsFile> parsed = JsonSupport.Deserialize<AccountsFile>(reader.ReadToEnd());
            if (!parsed.IsSuccess || parsed.Value is null)
            {
                return Result<AccountsFile>.Failure(
                    parsed.IsSuccess ? ErrorCode.ConfigInvalid : parsed.Code,
                    parsed.IsSuccess ? "账号文件为空。" : parsed.Detail);
            }

            return Result<AccountsFile>.Success(parsed.Value);
        }
        catch (IOException ex)
        {
            return Result<AccountsFile>.Failure(ErrorCode.IoError, ex.Message);
        }
    }

    private Result Write(AccountsFile file)
    {
        try
        {
            string? directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                _fileSystem.CreateDirectory(directory);
            }

            using Stream stream = _fileSystem.OpenWrite(_path, append: false);
            using StreamWriter writer = new(stream, Utf8NoBom);
            writer.Write(JsonSupport.Serialize(file));
            return Result.Ok;
        }
        catch (IOException ex)
        {
            return Result.Failure(ErrorCode.IoError, ex.Message);
        }
    }

    private sealed record AccountsFile(int SchemaVersion, IReadOnlyList<AccountProfile> Accounts);
}