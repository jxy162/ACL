using System.Globalization;
using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Version;

namespace ACL.Core.Launch;

/// <summary><see cref="ILaunchPlanner"/> 的默认实现：变量替换 → JVM/游戏参数 → 去重 → 计划。</summary>
public sealed class LaunchPlanner : ILaunchPlanner
{
    /// <inheritdoc />
    public Result<LaunchPlan> Plan(LaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        LaunchSettings settings = request.Settings;
        List<string> classpath = BuildClasspath(request);

        Dictionary<string, string> variables = BuildVariables(request, classpath);

        List<string> jvmArguments = BuildJvmArguments(request, variables);
        List<string> gameArguments = BuildGameArguments(request, variables);

        return Result<LaunchPlan>.Success(new LaunchPlan(
            JavaExecutable(request.Java),
            jvmArguments,
            gameArguments,
            settings.GameDirectory,
            settings.NativesDirectory,
            settings.ExtraEnvironment ?? new Dictionary<string, string>()));
    }

    private static string JavaExecutable(JavaRuntime java)
        => string.IsNullOrEmpty(java.HomeFolder)
            ? OperatingSystem.IsWindows() ? "java.exe" : "java"
            : Path.Combine(java.HomeFolder, "bin", OperatingSystem.IsWindows() ? "java.exe" : "java");

    private static List<string> BuildClasspath(LaunchRequest request)
    {
        List<string> classpath =
        [
            Path.Combine(request.Targets.VersionsDir, request.VersionId, request.VersionId + ".jar"),
        ];

        foreach (LibraryRef library in request.Version.Libraries)
        {
            if (!RuleEvaluator.IsAllowed(library.Rules, request.Os))
            {
                continue;
            }

            if (LibraryPaths.ArtifactRelativePath(library) is { } relative)
            {
                classpath.Add(Path.Combine(request.Targets.LibrariesDir, relative.Replace('/', Path.DirectorySeparatorChar)));
            }
        }

        return classpath;
    }

    private static Dictionary<string, string> BuildVariables(LaunchRequest request, IReadOnlyList<string> classpath)
    {
        LaunchSettings settings = request.Settings;
        AuthSession session = request.Session;

        Dictionary<string, string> variables = new(StringComparer.Ordinal)
        {
            ["auth_player_name"] = session.Name,
            ["auth_uuid"] = session.Uuid,
            ["auth_access_token"] = session.AccessToken,
            ["auth_session"] = $"token:{session.AccessToken}:{session.Uuid}",
            ["user_type"] = session.Type == AccountType.Offline ? "legacy" : "msa",
            ["user_properties"] = "{}",
            ["version_name"] = request.VersionId,
            ["version_type"] = settings.VersionType ?? request.Version.Type ?? "release",
            ["game_directory"] = settings.GameDirectory,
            ["assets_root"] = settings.AssetsRoot,
            ["assets_index_name"] = settings.AssetIndexId,
            ["game_assets"] = settings.AssetsRoot,
            ["natives_directory"] = settings.NativesDirectory,
            ["classpath"] = string.Join(Path.PathSeparator, classpath),
            ["classpath_separator"] = Path.PathSeparator.ToString(),
            ["launcher_name"] = settings.LauncherName,
            ["launcher_version"] = settings.LauncherVersion,
            ["resolution_width"] = settings.WindowWidth.ToString(CultureInfo.InvariantCulture),
            ["resolution_height"] = settings.WindowHeight.ToString(CultureInfo.InvariantCulture),
        };

        if (!string.IsNullOrEmpty(settings.LibrariesDirectory))
        {
            variables["library_directory"] = settings.LibrariesDirectory;
        }

        return variables;
    }

    private static List<string> BuildJvmArguments(LaunchRequest request, IReadOnlyDictionary<string, string> variables)
    {
        LaunchSettings settings = request.Settings;
        List<string> arguments = [];

        if (settings.MinMemoryMb > 0)
        {
            arguments.Add($"-Xms{settings.MinMemoryMb.ToString(CultureInfo.InvariantCulture)}m");
        }

        arguments.Add($"-Xmx{settings.MaxMemoryMb.ToString(CultureInfo.InvariantCulture)}m");

        foreach (ArgumentEntry entry in request.Version.JvmArguments)
        {
            if (RuleEvaluator.IsAllowed(entry.Rules, request.Os))
            {
                arguments.AddRange(entry.Values);
            }
        }

        if (request.Session.Type == AccountType.ThirdParty
            && settings.AuthlibInjectorJar is not null
            && settings.ThirdPartyApiRoot is not null)
        {
            arguments.Add(AuthlibInjector.BuildJavaAgentArgument(settings.AuthlibInjectorJar, settings.ThirdPartyApiRoot));
        }

        if (settings.ExtraJvmArguments is not null)
        {
            arguments.AddRange(settings.ExtraJvmArguments);
        }

        return ArgumentTemplating.DistinctPreservingOrder(ArgumentTemplating.Expand(arguments, variables));
    }

    private static List<string> BuildGameArguments(LaunchRequest request, IReadOnlyDictionary<string, string> variables)
    {
        List<string> arguments = [];

        if (request.Version.GameArguments.Count > 0)
        {
            foreach (ArgumentEntry entry in request.Version.GameArguments)
            {
                if (RuleEvaluator.IsAllowed(entry.Rules, request.Os))
                {
                    arguments.AddRange(entry.Values);
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.Version.LegacyMinecraftArguments))
        {
            arguments.AddRange(request.Version.LegacyMinecraftArguments
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        if (request.Settings.ExtraGameArguments is not null)
        {
            arguments.AddRange(request.Settings.ExtraGameArguments);
        }

        return ArgumentTemplating.DistinctPreservingOrder(ArgumentTemplating.Expand(arguments, variables));
    }
}