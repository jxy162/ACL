using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Version;
using Xunit;

namespace ACL.Core.Version.Tests;

public class VersionPlannerTests
{
    private const string Json = """
    {
      "id": "1.20.1",
      "mainClass": "net.minecraft.client.main.Main",
      "assets": "5",
      "assetIndex": { "id": "5", "url": "https://piston-meta.mojang.com/v1/packages/idx/5.json", "sha1": "idx", "size": 123 },
      "downloads": { "client": { "url": "https://piston-data.mojang.com/v1/objects/abc/client.jar", "sha1": "abc", "size": 999 } },
      "javaVersion": { "component": "java-runtime-gamma", "majorVersion": 17 },
      "libraries": [
        { "name": "com.mojang:logging:1.1.1", "downloads": { "artifact": { "path": "com/mojang/logging/1.1.1/logging-1.1.1.jar", "url": "https://libraries.minecraft.net/com/mojang/logging/1.1.1/logging-1.1.1.jar", "sha1": "lg", "size": 50 } } },
        { "name": "org.lwjgl:lwjgl:3.3.1", "natives": { "windows": "natives-windows" }, "downloads": { "artifact": { "path": "org/lwjgl/lwjgl/3.3.1/lwjgl-3.3.1.jar", "url": "https://libraries.minecraft.net/org/lwjgl/lwjgl/3.3.1/lwjgl-3.3.1.jar", "sha1": "lw", "size": 70 }, "classifiers": { "natives-windows": { "path": "org/lwjgl/lwjgl/3.3.1/lwjgl-3.3.1-natives-windows.jar", "url": "https://libraries.minecraft.net/org/lwjgl/lwjgl/3.3.1/lwjgl-3.3.1-natives-windows.jar", "sha1": "lwn", "size": 20 } } } },
        { "name": "net.minecraft:serveronly:1.0", "url": "https://libraries.minecraft.net/", "rules": [ { "action": "allow", "os": { "name": "linux" } } ] }
      ]
    }
    """;

    private static readonly InstallTargets Targets = InstallTargets.ForGameDir("D:/mc");

    [Fact]
    public void Plan_IncludesClientIndexLibrariesAndNatives()
    {
        VersionJson version = VersionJsonParser.Parse(Json).Value!;

        InstallPlan plan = new VersionPlanner().Plan(version, Targets, OsSpec.WindowsX64);

        Assert.Equal("1.20.1", plan.VersionId);
        Assert.Equal(5, plan.Downloads.Count);
        Assert.Contains(plan.Downloads, d => d.TargetPath == Path.Combine(Targets.VersionsDir, "1.20.1", "1.20.1.jar"));
        Assert.Contains(plan.Downloads, d => d.TargetPath == Path.Combine(Targets.AssetsDir, "indexes", "5.json"));
        Assert.Contains(plan.Downloads, d => d.TargetPath == Path.Combine(Targets.LibrariesDir, "com", "mojang", "logging", "1.1.1", "logging-1.1.1.jar"));
        Assert.Contains(plan.Downloads, d => d.TargetPath.EndsWith("lwjgl-3.3.1-natives-windows.jar", StringComparison.Ordinal));
        Assert.DoesNotContain(plan.Downloads, d => d.TargetPath.Contains("serveronly", StringComparison.Ordinal));
        Assert.Empty(plan.Warnings);
    }

    [Fact]
    public void Plan_OnLinux_ExcludesWindowsNatives()
    {
        VersionJson version = VersionJsonParser.Parse(Json).Value!;

        InstallPlan plan = new VersionPlanner().Plan(version, Targets, OsSpec.LinuxX64);

        Assert.DoesNotContain(plan.Downloads, d => d.TargetPath.EndsWith("natives-windows.jar", StringComparison.Ordinal));
        Assert.Contains(plan.Downloads, d => d.TargetPath.Contains("serveronly", StringComparison.Ordinal));
    }

    [Fact]
    public void Plan_MissingClient_AddsWarning()
    {
        VersionJson version = VersionJsonParser.Parse("""{ "id": "x", "libraries": [] }""").Value!;

        InstallPlan plan = new VersionPlanner().Plan(version, Targets, OsSpec.WindowsX64);

        Assert.Empty(plan.Downloads);
        Assert.NotEmpty(plan.Warnings);
    }

    [Fact]
    public void Plan_DerivesLibraryPathFromUrl_WhenArtifactMissing()
    {
        const string json = """{ "id": "x", "libraries": [ { "name": "com.example:lib:2.0", "url": "https://maven.example.com/" } ] }""";
        VersionJson version = VersionJsonParser.Parse(json).Value!;

        InstallPlan plan = new VersionPlanner().Plan(version, Targets, OsSpec.WindowsX64);

        DownloadItem item = Assert.Single(plan.Downloads);
        Assert.Equal(new Uri("https://maven.example.com/com/example/lib/2.0/lib-2.0.jar"), item.Primary);
        Assert.Equal(Path.Combine(Targets.LibrariesDir, "com", "example", "lib", "2.0", "lib-2.0.jar"), item.TargetPath);
    }
}