using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Version;
using Xunit;

namespace ACL.Core.Version.Tests;

public class VersionJsonParserTests
{
    internal const string ChildJson = """
    {
      "id": "1.20.1-forge-47.2.0",
      "inheritsFrom": "1.20.1",
      "mainClass": "cpw.mods.bootstraplauncher.BootstrapLauncher",
      "type": "release",
      "arguments": {
        "game": [ "--launchTarget", "forgeclient" ],
        "jvm": [ "-Dforge.logging.console.level=info", { "rules": [ { "action": "allow", "os": { "name": "windows" } } ], "value": [ "-Dos.name=Windows" ] } ]
      },
      "libraries": [
        { "name": "net.minecraftforge:forge:1.20.1-47.2.0", "downloads": { "artifact": { "path": "net/minecraftforge/forge/1.20.1-47.2.0/forge-1.20.1-47.2.0.jar", "url": "https://maven.minecraftforge.net/net/minecraftforge/forge/1.20.1-47.2.0/forge-1.20.1-47.2.0.jar", "sha1": "ffff", "size": 100 } } }
      ]
    }
    """;

    internal const string ParentJson = """
    {
      "id": "1.20.1",
      "mainClass": "net.minecraft.client.main.Main",
      "assets": "5",
      "assetIndex": { "id": "5", "url": "https://piston-meta.mojang.com/v1/packages/idx/5.json", "sha1": "idx", "size": 123, "totalSize": 456 },
      "javaVersion": { "component": "java-runtime-gamma", "majorVersion": 17 },
      "downloads": { "client": { "url": "https://piston-data.mojang.com/v1/objects/abc/client.jar", "sha1": "abc", "size": 999 } },
      "arguments": { "game": [ "--username", "${auth_player_name}" ], "jvm": [ "-cp", "${classpath}" ] },
      "libraries": [
        { "name": "com.mojang:logging:1.1.1", "downloads": { "artifact": { "path": "com/mojang/logging/1.1.1/logging-1.1.1.jar", "url": "https://libraries.minecraft.net/com/mojang/logging/1.1.1/logging-1.1.1.jar", "sha1": "lg", "size": 50 } } },
        { "name": "org.lwjgl:lwjgl:3.3.1", "natives": { "windows": "natives-windows${arch}" }, "downloads": { "artifact": { "path": "org/lwjgl/lwjgl/3.3.1/lwjgl-3.3.1.jar", "url": "https://libraries.minecraft.net/org/lwjgl/lwjgl/3.3.1/lwjgl-3.3.1.jar", "sha1": "lw", "size": 70 }, "classifiers": { "natives-windows": { "path": "org/lwjgl/lwjgl/3.3.1/lwjgl-3.3.1-natives-windows.jar", "url": "https://libraries.minecraft.net/org/lwjgl/lwjgl/3.3.1/lwjgl-3.3.1-natives-windows.jar", "sha1": "lwn", "size": 20 } } } }
      ]
    }
    """;

    [Fact]
    public void Parse_ChildJson_ReadsInheritsFromAndArguments()
    {
        Result<VersionJson> result = VersionJsonParser.Parse(ChildJson);

        Assert.True(result.IsSuccess, result.Code + ": " + result.Detail);
        VersionJson json = result.Value!;
        Assert.Equal("1.20.1-forge-47.2.0", json.Id);
        Assert.Equal("1.20.1", json.InheritsFrom);
        Assert.Single(json.Libraries);
        Assert.Equal(2, json.GameArguments.Count);
        Assert.Equal(2, json.JvmArguments.Count);
        Assert.NotNull(json.JvmArguments[1].Rules);
    }

    [Fact]
    public void Parse_ParentJson_ReadsAssetsIndexAndClassifiers()
    {
        Result<VersionJson> result = VersionJsonParser.Parse(ParentJson);

        Assert.True(result.IsSuccess, result.Code + ": " + result.Detail);
        VersionJson json = result.Value!;
        Assert.Equal("5", json.AssetIndex!.Id);
        Assert.Equal(17, json.JavaVersion!.MajorVersion);
        Assert.Equal(999, json.Downloads!.Client!.Size);
        LibraryRef lwjgl = json.Libraries.Single(l => l.Name.StartsWith("org.lwjgl:lwjgl", StringComparison.Ordinal));
        Assert.NotNull(lwjgl.Natives);
        Assert.Equal("natives-windows64", lwjgl.Natives!["windows"]);
        Assert.True(lwjgl.Downloads!.Classifiers!.ContainsKey("natives-windows"));
    }

    [Fact]
    public void Parse_InvalidJson_Fails()
    {
        Assert.False(VersionJsonParser.Parse("###").IsSuccess);
        Assert.Equal(ErrorCode.ManifestCorrupt, VersionJsonParser.Parse("{}").Code);
    }
}