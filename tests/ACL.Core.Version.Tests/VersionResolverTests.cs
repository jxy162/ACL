using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Version;
using Xunit;

namespace ACL.Core.Version.Tests;

public class VersionResolverTests
{
    [Fact]
    public void Merge_CombinesLibrariesAndArguments_ChildWinsScalars()
    {
        VersionJson parent = VersionJsonParser.Parse(VersionJsonParserTests.ParentJson).Value!;
        VersionJson child = VersionJsonParser.Parse(VersionJsonParserTests.ChildJson).Value!;

        VersionJson merged = VersionResolver.Merge([parent, child]);

        Assert.Equal("cpw.mods.bootstraplauncher.BootstrapLauncher", merged.MainClass);
        Assert.Equal("5", merged.AssetIndex!.Id);                 // 标量继承父
        Assert.Equal(17, merged.JavaVersion!.MajorVersion);
        Assert.Equal(3, merged.Libraries.Count);                   // 父 2 + 子 1
        Assert.Equal(4, merged.GameArguments.Count);               // 父 2 + 子 2
        Assert.Equal(4, merged.JvmArguments.Count);                // 父 2 + 子 2
        Assert.Null(merged.InheritsFrom);                          // 已解析完成
    }

    [Fact]
    public void Merge_SameLibraryName_ChildOverrides()
    {
        LibraryRef parentLib = new("com.example:lib:1.0", null, "https://parent");
        LibraryRef childLib = new("com.example:lib:1.0", null, "https://child");
        VersionJson parent = MakeJson("p", null, [parentLib]);
        VersionJson child = MakeJson("c", "p", [childLib]);

        VersionJson merged = VersionResolver.Merge([parent, child]);

        Assert.Single(merged.Libraries);
        Assert.Equal("https://child", merged.Libraries[0].Url);
    }

    [Fact]
    public async Task ResolveAsync_FollowsChain()
    {
        Dictionary<string, string> files = new(StringComparer.Ordinal)
        {
            ["1.20.1-forge-47.2.0"] = VersionJsonParserTests.ChildJson,
            ["1.20.1"] = VersionJsonParserTests.ParentJson,
        };

        Result<VersionJson> result = await VersionResolver.ResolveAsync(
            "1.20.1-forge-47.2.0",
            (id, _) => Task.FromResult(files.TryGetValue(id, out string? json)
                ? VersionJsonParser.Parse(json)
                : Result<VersionJson>.Failure(ErrorCode.NotFound, id)));

        Assert.True(result.IsSuccess, result.Code + ": " + result.Detail);
        Assert.Equal(3, result.Value!.Libraries.Count);
        Assert.Equal("cpw.mods.bootstraplauncher.BootstrapLauncher", result.Value!.MainClass);
    }

    [Fact]
    public async Task ResolveAsync_DetectsCycle()
    {
        VersionJson self = MakeJson("a", "a", []);

        Result<VersionJson> result = await VersionResolver.ResolveAsync(
            "a",
            (_, _) => Task.FromResult(Result<VersionJson>.Success(self)));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.ManifestCorrupt, result.Code);
    }

    private static VersionJson MakeJson(string id, string? inheritsFrom, IReadOnlyList<LibraryRef> libraries)
        => new(id, inheritsFrom, "Main", "5", "release", null, null, null, libraries, [], [], null);
}