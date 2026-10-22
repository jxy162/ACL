using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Java;

/// <summary>
/// <see cref="IJavaLocator"/> 的默认实现：在每个搜索根下按 <c>&lt;dir&gt;/bin/java</c>（含一、二级子目录）发现候选，
/// 过滤掉 <c>system32</c> / <c>javapath_target_*</c> / <c>javatmp</c> 等特殊路径后并行探测，按版本降序去重返回。
/// </summary>
public sealed class JavaLocator : IJavaLocator
{
    private static readonly string[] ExcludedFragments =
    [
        "system32",
        "javapath_target_",
        "javatmp",
        "javapath",
    ];

    private readonly IFileSystem _fileSystem;
    private readonly IJavaProbe _probe;
    private readonly IReadOnlyList<string> _searchRoots;
    private readonly string _executableName;

    /// <summary>构造搜索器。</summary>
    /// <param name="fileSystem">文件系统。</param>
    /// <param name="probe">Java 探测器。</param>
    /// <param name="searchRoots">搜索根目录（如 JAVA_HOME、<c>Program Files/Java</c>）。</param>
    /// <param name="executableName">可执行文件名；为 <see langword="null"/> 时按平台推断。</param>
    public JavaLocator(
        IFileSystem fileSystem,
        IJavaProbe probe,
        IReadOnlyList<string> searchRoots,
        string? executableName = null)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _searchRoots = searchRoots ?? throw new ArgumentNullException(nameof(searchRoots));
        _executableName = executableName ?? (OperatingSystem.IsWindows() ? "java.exe" : "java");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<JavaRuntime>> LocateAsync(CancellationToken cancellationToken = default)
    {
        List<string> candidates = [];
        foreach (string root in _searchRoots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            AddCandidate(candidates, root);
            foreach (string level1 in SafeEnumerateDirectories(root))
            {
                AddCandidate(candidates, level1);
                foreach (string level2 in SafeEnumerateDirectories(level1))
                {
                    AddCandidate(candidates, level2);
                }
            }
        }

        List<string> filtered = candidates
            .Where(path => !IsExcluded(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Task<Result<JavaRuntime>>[] probes = [.. filtered.Select(path => _probe.ProbeAsync(path, cancellationToken))];
        Result<JavaRuntime>[] results = await Task.WhenAll(probes).ConfigureAwait(false);

        return
        [
            .. results.Where(result => result.IsSuccess)
                .Select(result => result.Value!)
                .GroupBy(runtime => runtime.HomeFolder, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(runtime => runtime.Version).First())
                .OrderByDescending(runtime => runtime.Version)
        ];
    }

    private void AddCandidate(List<string> candidates, string directory)
    {
        string executable = directory.EndsWith("bin", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(directory, _executableName)
            : Path.Combine(directory, "bin", _executableName);

        if (_fileSystem.FileExists(executable))
        {
            candidates.Add(executable);
        }
    }

    private IEnumerable<string> SafeEnumerateDirectories(string directory)
    {
        try
        {
            return _fileSystem.DirectoryExists(directory) ? _fileSystem.EnumerateDirectories(directory) : [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    private static bool IsExcluded(string path)
        => ExcludedFragments.Any(fragment => path.Contains(fragment, StringComparison.OrdinalIgnoreCase));
}