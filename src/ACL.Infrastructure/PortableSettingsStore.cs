using System.Text;
using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Infrastructure;

/// <summary>基于文件系统的便携设置存储（<c>ACL.config.json</c>，UTF-8 无 BOM）。</summary>
public sealed class PortableSettingsStore : IAppSettingsStore
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly IFileSystem _fileSystem;
    private readonly string _path;

    /// <summary>构造存储。</summary>
    public PortableSettingsStore(IFileSystem fileSystem, string path)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    /// <inheritdoc />
    public Result<AclSettings> Load()
    {
        try
        {
            if (!_fileSystem.FileExists(_path))
            {
                return Result<AclSettings>.Success(new AclSettings());
            }

            using Stream stream = _fileSystem.OpenRead(_path);
            using StreamReader reader = new(stream, Utf8NoBom);
            Result<AclSettings> parsed = JsonSupport.Deserialize<AclSettings>(reader.ReadToEnd());
            return parsed.IsSuccess ? parsed : Result<AclSettings>.Success(new AclSettings());
        }
        catch (IOException ex)
        {
            return Result<AclSettings>.Failure(ErrorCode.IoError, ex.Message);
        }
    }

    /// <inheritdoc />
    public Result Save(AclSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            string? directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                _fileSystem.CreateDirectory(directory);
            }

            using Stream stream = _fileSystem.OpenWrite(_path, append: false);
            using StreamWriter writer = new(stream, Utf8NoBom);
            writer.Write(JsonSupport.Serialize(settings));
            return Result.Ok;
        }
        catch (IOException ex)
        {
            return Result.Failure(ErrorCode.IoError, ex.Message);
        }
    }
}