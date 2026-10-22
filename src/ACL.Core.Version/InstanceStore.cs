using System.Text;
using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Version;

/// <summary>基于 <see cref="IFileSystem"/> 的 <see cref="IInstanceStore"/> 实现。</summary>
public sealed class InstanceStore : IInstanceStore
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly IFileSystem _fileSystem;

    /// <summary>构造实例存储。</summary>
    public InstanceStore(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    /// <inheritdoc />
    public Result<InstallManifest> LoadManifest(string path) => Load<InstallManifest>(path);

    /// <inheritdoc />
    public Result SaveManifest(string path, InstallManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return Save(path, manifest);
    }

    /// <inheritdoc />
    public Result<InstanceDescriptor> LoadDescriptor(string path) => Load<InstanceDescriptor>(path);

    /// <inheritdoc />
    public Result SaveDescriptor(string path, InstanceDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return Save(path, descriptor);
    }

    private Result<T> Load<T>(string path)
    {
        try
        {
            if (!_fileSystem.FileExists(path))
            {
                return Result<T>.Failure(ErrorCode.NotFound, path);
            }

            using Stream stream = _fileSystem.OpenRead(path);
            using StreamReader reader = new(stream, Utf8NoBom);
            string json = reader.ReadToEnd();
            return InstanceJson.Deserialize<T>(json);
        }
        catch (IOException ex)
        {
            return Result<T>.Failure(ErrorCode.IoError, ex.Message);
        }
    }

    private Result Save<T>(string path, T value)
    {
        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                _fileSystem.CreateDirectory(directory);
            }

            using Stream stream = _fileSystem.OpenWrite(path, append: false);
            using StreamWriter writer = new(stream, Utf8NoBom);
            writer.Write(InstanceJson.Serialize(value));
            return Result.Ok;
        }
        catch (IOException ex)
        {
            return Result.Failure(ErrorCode.IoError, ex.Message);
        }
    }
}