using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Launch;
using ACL.Core.Version;
using Xunit;

namespace ACL.Core.Launch.Tests;

public class LaunchPlannerTests
{
    private static readonly InstallTargets Targets = InstallTargets.ForGameDir("D:/mc");
    private static readonly JavaRuntime Java = new("C:/Java/jdk-17", new System.Version(17, 0, 9), true);
    private static readonly OsSpec Windows = OsSpec.WindowsX64;

    [Fact]
    public void Plan_ExpandsPlaceholdersAndAddsMemoryFlags()
    {
        LaunchPlan plan = PlanFor(Version(), Offline("Steve"));

        Assert.Contains("-Xms512m", plan.JvmArgs);
        Assert.Contains("-Xmx2048m", plan.JvmArgs);
        Assert.Contains("--username", plan.GameArgs);
        Assert.Contains("Steve", plan.GameArgs);
        Assert.Contains("--version", plan.GameArgs);
        Assert.Contains("1.20.1", plan.GameArgs);
        Assert.Equal(Path.Combine("C:/Java/jdk-17", "bin", OperatingSystem.IsWindows() ? "java.exe" : "java"), plan.JavaPath);
        Assert.Equal("D:/mc", plan.WorkingDir);
    }

    [Fact]
    public void Plan_ClasspathContainsClientAndLibraries()
    {
        LaunchPlan plan = PlanFor(Version(), Offline("Steve"));

        int cpIndex = plan.JvmArgs.ToList().IndexOf("-cp");
        Assert.True(cpIndex >= 0);
        string classpath = plan.JvmArgs[cpIndex + 1];
        string separator = Path.PathSeparator.ToString();
        Assert.Contains(Path.Combine(Targets.VersionsDir, "1.20.1", "1.20.1.jar"), classpath);
        Assert.Contains(Path.Combine(Targets.LibrariesDir, "com", "mojang", "logging", "1.1.1", "logging-1.1.1.jar"), classpath);
        Assert.Equal(2, classpath.Split(separator).Length);
    }

    [Fact]
    public void Plan_DeduplicatesJvmArguments()
    {
        VersionJson version = Version(
            jvm:
            [
                new ArgumentEntry(["-Djava.library.path=${natives_directory}"]),
                new ArgumentEntry(["-Djava.library.path=ignored"]),
                new ArgumentEntry(["-Xmx1g"]),
            ]);

        LaunchPlan plan = PlanFor(version, Offline("Steve"));

        Assert.Single(plan.JvmArgs, a => a.Contains("java.library.path", StringComparison.Ordinal));
        Assert.Contains("-Xmx2048m", plan.JvmArgs);
        Assert.DoesNotContain("-Xmx1g", plan.JvmArgs);
    }

    [Fact]
    public void Plan_ThirdParty_AddsAuthlibInjector()
    {
        LaunchSettings settings = Settings() with
        {
            AuthlibInjectorJar = "C:/acl/authlib-injector.jar",
            ThirdPartyApiRoot = "https://littleskin.cn/api/yggdrasil",
        };
        AuthSession thirdParty = new(AccountType.ThirdParty, "uuid", "Player", "token");

        LaunchPlan plan = PlanFor(Version(), thirdParty, settings);

        Assert.Contains(
            "-javaagent:C:/acl/authlib-injector.jar=https://littleskin.cn/api/yggdrasil",
            plan.JvmArgs);
    }

    [Fact]
    public void Plan_LegacyMinecraftArguments_IsSplit()
    {
        VersionJson version = new(
            "1.6.4", null, "net.minecraft.client.Main", "legacy", "release", null, null, null, [],
            [], [], "--username ${auth_player_name} --gameDir ${game_directory}");

        LaunchPlan plan = PlanFor(version, Offline("Steve"));

        Assert.Equal(["--username", "Steve", "--gameDir", "D:/mc"], plan.GameArgs);
    }

    [Fact]
    public void Plan_RulesFilterArgumentsByOs()
    {
        VersionJson version = Version(
            jvm:
            [
                new ArgumentEntry(["-Dos=windows"], [new RuleEntry("allow", "windows")]),
                new ArgumentEntry(["-Dos=linux"], [new RuleEntry("allow", "linux")]),
            ]);

        LaunchPlan plan = PlanFor(version, Offline("Steve"));

        Assert.Contains("-Dos=windows", plan.JvmArgs);
        Assert.DoesNotContain("-Dos=linux", plan.JvmArgs);
    }

    private static LaunchPlan PlanFor(VersionJson version, AuthSession session, LaunchSettings? settings = null)
    {
        LaunchRequest request = new("1.20.1", version, Targets, Java, session, settings ?? Settings(), Windows);
        Result<LaunchPlan> result = new LaunchPlanner().Plan(request);
        Assert.True(result.IsSuccess, result.Code + ": " + result.Detail);
        return result.Value!;
    }

    private static LaunchSettings Settings() => new()
    {
        GameDirectory = "D:/mc",
        AssetsRoot = "D:/mc/assets",
        AssetIndexId = "5",
        NativesDirectory = "D:/mc/versions/1.20.1/natives",
        LibrariesDirectory = "D:/mc/libraries",
    };

    private static AuthSession Offline(string name)
        => new(AccountType.Offline, "uuid-" + name, name, "0");

    private static VersionJson Version(
        IReadOnlyList<LibraryRef>? libraries = null,
        IReadOnlyList<ArgumentEntry>? jvm = null,
        IReadOnlyList<ArgumentEntry>? game = null)
        => new(
            "1.20.1",
            null,
            "net.minecraft.client.main.Main",
            "5",
            "release",
            null,
            null,
            null,
            libraries ?? [new LibraryRef("com.mojang:logging:1.1.1")],
            game ?? [new ArgumentEntry(["--username", "${auth_player_name}"]), new ArgumentEntry(["--version", "${version_name}"])],
            jvm ?? [new ArgumentEntry(["-cp", "${classpath}"])],
            null);
}
