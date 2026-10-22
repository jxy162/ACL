using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Version;
using Xunit;

namespace ACL.Core.Version.Tests;

public class VersionJsonWriterTests
{
    [Fact]
    public void Write_ThenParse_RoundTripsCoreFields()
    {
        VersionJson original = new(
            "my-instance",
            "1.20.1",
            "net.fabricmc.loader.impl.launch.knot.KnotClient",
            null,
            "release",
            new AssetIndexRef("5", new Uri("https://example.com/5.json"), "abc", 10, 100),
            new JavaVersionRef("java-runtime-gamma", 17),
            new VersionDownloads(new DownloadArtifact(new Uri("https://example.com/client.jar"), "def", 2048)),
            [
                new LibraryRef(
                    "net.fabricmc:fabric-loader:0.15.11",
                    new LibraryDownloads(new DownloadArtifact(new Uri("https://example.com/fl.jar"), "aaa", 100, "net/fabricmc/fabric-loader/0.15.11/fabric-loader-0.15.11.jar")),
                    "https://maven.fabricmc.net/",
                    null,
                    [new RuleEntry("allow", "windows")]),
            ],
            [new ArgumentEntry(["--username", "${auth_player_name}"])],
            [new ArgumentEntry(["-Djava.library.path=${natives_directory}"])],
            null);

        string json = VersionJsonWriter.Write(original);

        Result<VersionJson> parsed = VersionJsonParser.Parse(json);

        Assert.True(parsed.IsSuccess);
        VersionJson round = parsed.Value!;
        Assert.Equal("my-instance", round.Id);
        Assert.Equal("1.20.1", round.InheritsFrom);
        Assert.Equal(original.MainClass, round.MainClass);
        Assert.Equal("java-runtime-gamma", round.JavaVersion!.Component);
        Assert.Equal(17, round.JavaVersion.MajorVersion);
        Assert.Equal(new Uri("https://example.com/client.jar"), round.Downloads!.Client!.Url);
        Assert.Equal("abc", round.AssetIndex!.Sha1);
        Assert.Single(round.Libraries);
        Assert.Equal("net.fabricmc:fabric-loader:0.15.11", round.Libraries[0].Name);
        Assert.Equal("windows", round.Libraries[0].Rules![0].OsName);
        Assert.Equal(2, round.GameArguments[0].Values.Count);
        Assert.Equal("-Djava.library.path=${natives_directory}", round.JvmArguments[0].Values[0]);
    }

    [Fact]
    public void Write_MinimalVersion_ProducesValidJson()
    {
        VersionJson version = new("x", null, null, null, null, null, null, null, [], [], [], null);

        string json = VersionJsonWriter.Write(version);

        Result<VersionJson> parsed = VersionJsonParser.Parse(json);
        Assert.True(parsed.IsSuccess);
        Assert.Equal("x", parsed.Value!.Id);
    }
}