using System.Globalization;
using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Java;

/// <summary><see cref="IJavaProbe"/> 的默认实现。</summary>
public sealed class JavaProbe : IJavaProbe
{
    private readonly IProcessLauncher _processLauncher;
    private readonly IFileSystem _fileSystem;

    /// <summary>构造探测器。</summary>
    public JavaProbe(IProcessLauncher processLauncher, IFileSystem fileSystem)
    {
        _processLauncher = processLauncher ?? throw new ArgumentNullException(nameof(processLauncher));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    /// <inheritdoc />
    public async Task<Result<JavaRuntime>> ProbeAsync(string javaExecutablePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(javaExecutablePath))
        {
            return Result<JavaRuntime>.Failure(ErrorCode.NotFound, "Java 路径为空。");
        }

        if (!_fileSystem.FileExists(javaExecutablePath))
        {
            return Result<JavaRuntime>.Failure(ErrorCode.NotFound, javaExecutablePath);
        }

        Result<ProcessResult> run = await _processLauncher
            .RunAsync(new ProcessSpec(javaExecutablePath, ["-XshowSettings:properties", "-version"]), cancellationToken)
            .ConfigureAwait(false);
        if (!run.IsSuccess)
        {
            return Result<JavaRuntime>.Failure(run.Code, run.Detail);
        }

        string output = run.Value!.CombinedOutput;
        Dictionary<string, string> properties = ParseProperties(output);

        string? versionText = properties.GetValueOrDefault("java.version") ?? ExtractVersionFromBanner(output);
        if (versionText is null || !JavaVersionParser.TryParse(versionText, out Version version))
        {
            return Result<JavaRuntime>.Failure(ErrorCode.JavaMissing, "无法解析 Java 版本输出。");
        }

        bool is64Bit = properties.GetValueOrDefault("sun.arch.data.model") == "64";
        string home = properties.GetValueOrDefault("java.home") ?? DeriveHome(javaExecutablePath);

        return Result<JavaRuntime>.Success(new JavaRuntime(
            home,
            version,
            is64Bit,
            properties.GetValueOrDefault("file.encoding"),
            properties.GetValueOrDefault("sun.jnu.encoding")));
    }

    private static Dictionary<string, string> ParseProperties(string output)
    {
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        foreach (string line in output.Split('\n'))
        {
            int equals = line.IndexOf('=', StringComparison.Ordinal);
            if (equals <= 0)
            {
                continue;
            }

            string key = line[..equals].Trim();
            if (key.Length == 0 || key.Contains(' ', StringComparison.Ordinal))
            {
                continue;
            }

            map[key] = line[(equals + 1)..].Trim();
        }

        return map;
    }

    private static string? ExtractVersionFromBanner(string output)
    {
        foreach (string line in output.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("openjdk version", StringComparison.Ordinal)
                || trimmed.StartsWith("java version", StringComparison.Ordinal))
            {
                int first = trimmed.IndexOf('"', StringComparison.Ordinal);
                int last = trimmed.LastIndexOf('"');
                if (first >= 0 && last > first)
                {
                    return trimmed[(first + 1)..last];
                }
            }
        }

        return null;
    }

    private static string DeriveHome(string javaExecutablePath)
    {
        string? bin = Path.GetDirectoryName(javaExecutablePath);
        return bin is null ? javaExecutablePath : Path.GetDirectoryName(bin) ?? bin;
    }
}