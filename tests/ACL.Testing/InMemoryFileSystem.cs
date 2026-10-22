using ACL.Core.Abstractions;

namespace ACL.Testing;

/// <summary>内存文件系统桩。</summary>
public sealed class InMemoryFileSystem : IFileSystem
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);
    private readonly HashSet<string> _directories = new(StringComparer.Ordinal);

    public void WriteFile(string path, byte[] content)
    {
        _files[Key(path)] = content;
        RegisterParents(path);
    }

    public void WriteFile(string path, string content) => WriteFile(path, System.Text.Encoding.UTF8.GetBytes(content));

    public byte[] ReadAllBytes(string path) => _files[Key(path)];

    public bool FileExists(string path) => _files.ContainsKey(Key(path));

    public bool DirectoryExists(string path) => _directories.Contains(Key(path));

    public void CreateDirectory(string path) => _directories.Add(Key(path));

    public long GetFileLength(string path) => _files[Key(path)].LongLength;

    public DateTimeOffset GetLastWriteTimeUtc(string path) => DateTimeOffset.UnixEpoch;

    public Stream OpenRead(string path) => new MemoryStream(_files[Key(path)], writable: false);

    public Stream OpenWrite(string path, bool append = false)
    {
        string key = Key(path);
        RegisterParents(path);
        byte[]? initial = append && _files.TryGetValue(key, out byte[]? existing) ? existing : null;
        return new CommittingStream(initial, bytes => _files[key] = bytes);
    }

    public void Delete(string path) => _files.Remove(Key(path));

    public void Move(string sourcePath, string destinationPath, bool overwrite = true)
    {
        string from = Key(sourcePath);
        string to = Key(destinationPath);
        if (_files.TryGetValue(from, out byte[]? value))
        {
            _files[to] = value;
            _files.Remove(from);
            RegisterParents(destinationPath);
        }
    }

    public void Copy(string sourcePath, string destinationPath, bool overwrite = true)
    {
        _files[Key(destinationPath)] = _files[Key(sourcePath)];
        RegisterParents(destinationPath);
    }

    public IEnumerable<string> EnumerateFiles(string path, bool recursive = false) => _files.Keys;

    public IEnumerable<string> EnumerateDirectories(string path, bool recursive = false)
    {
        string prefix = Key(path) + "/";
        return _directories.Where(d => d.StartsWith(prefix, StringComparison.Ordinal) || d == Key(path));
    }

    private static string Key(string path) => path.Replace('\\', '/').TrimEnd('/');

    private void RegisterParents(string path)
    {
        string? directory = Path.GetDirectoryName(path);
        while (!string.IsNullOrEmpty(directory))
        {
            _directories.Add(Key(directory));
            string? parent = Path.GetDirectoryName(directory);
            if (parent == directory)
            {
                break;
            }

            directory = parent;
        }
    }

    private sealed class CommittingStream : MemoryStream
    {
        private readonly Action<byte[]> _commit;

        public CommittingStream(byte[]? initial, Action<byte[]> commit)
        {
            _commit = commit;
            if (initial is not null)
            {
                Write(initial, 0, initial.Length);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _commit(ToArray());
            }

            base.Dispose(disposing);
        }
    }
}