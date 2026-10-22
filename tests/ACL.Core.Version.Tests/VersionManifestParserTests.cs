using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Version;
using Xunit;

namespace ACL.Core.Version.Tests;

public class VersionManifestParserTests
{
    internal const string SampleManifest = """
    {
      "latest": { "release": "1.21.5", "snapshot": "26.1-snapshot-1" },
      "versions": [
        {
          "id": "1.21.5",
          "type": "release",
          "url": "https://piston-meta.mojang.com/v1/packages/aaa/1.21.5.json",
          "time": "2025-03-25T12:00:00+00:00",
          "releaseTime": "2025-03-25T11:00:00+00:00",
          "sha1": "aaaa",
          "complianceLevel": 1
        },
        {
          "id": "26.1-snapshot-1",
          "type": "snapshot",
          "url": "https://piston-meta.mojang.com/v1/packages/bbb/26.1-snapshot-1.json",
          "time": "2026-01-05T10:00:00+00:00",
          "releaseTime": "2026-01-05T09:00:00+00:00",
          "sha1": "bbbb",
          "complianceLevel": 1
        }
      ]
    }
    """;

    [Fact]
    public void Parse_ReadsLatestAndVersions()
    {
        Result<VersionManifest> result = VersionManifestParser.Parse(SampleManifest);

        Assert.True(result.IsSuccess);
        VersionManifest manifest = result.Value!;
        Assert.Equal("1.21.5", manifest.Latest.Release);
        Assert.Equal("26.1-snapshot-1", manifest.Latest.Snapshot);
        Assert.Equal(2, manifest.Versions.Count);
        Assert.Equal(new Uri("https://piston-meta.mojang.com/v1/packages/aaa/1.21.5.json"), manifest.Versions[0].Url);
        Assert.Equal("release", manifest.Versions[0].Type);
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsParseFailure()
    {
        Result<VersionManifest> result = VersionManifestParser.Parse("{ not json ]");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.VersionParseFailed, result.Code);
    }

    [Fact]
    public void Parse_MissingSections_ReturnsCorrupt()
    {
        Result<VersionManifest> result = VersionManifestParser.Parse("""{ "latest": { "release": "1.0" } }""");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.ManifestCorrupt, result.Code);
    }
}