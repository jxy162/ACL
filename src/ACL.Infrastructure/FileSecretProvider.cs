using ACL.Core.Abstractions;

namespace ACL.Infrastructure;

/// <summary>从便携 JSON 文件（<c>{ "KEY": "value" }</c>）读取密钥；文件不存在时视为无密钥。</summary>
public sealed class FileSecretProvider : ISecretProvider
{
    private readonly IFileSystem _fileSystem;
    private readonly string _path;
    private Dictionary<string, string>? _cache;
    private bool _loaded;

    /// <summary>构造提供方。</summary>
    /// <param name="fileSystem">文件系统。</param>
    /// <param name="path">密钥文件路径（建议与启动器同目录，且不要提交到仓库）。</param>
    public FileSecretProvider(IFileSystem fileSystem, string path)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    /// <inheritdoc />
    public string? GetSecret(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!_loaded)
        {
            _loaded = true;
            try
            {
                if (_fileSystem.FileExists(_path))
                {
                    using Stream stream = _fileSystem.OpenRead(_path);
                    using StreamReader reader = new(stream);
                    Result<Dictionary<string, string>> parsed = JsonSupport.Deserialize<Dictionary<string, string>>(reader.ReadToEnd());
                    _cache = parsed.IsSuccess ? parsed.Value : null;
                }
            }
            catch (IOException)
            {
                _cache = null;
            }
        }

        if (_cache is not null && _cache.TryGetValue(key, out string? value) && !string.IsNullOrEmpty(value))
        {
            return value;
        }

        return null;
    }
}