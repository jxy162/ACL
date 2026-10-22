using System.IO.Compression;
using System.Text;
using ACL.Core.Abstractions;
using ACL.Core.Launch;
using ACL.Testing;
using Xunit;

namespace ACL.Core.Launch.Tests;

public class NativesExtractorTests
{
    private const string Jar = "D:/mc/libraries/org/lwjgl/lwjgl/3.3.1/lwjgl-natives-windows.jar";
    private const string Natives = "D:/mc/versions/1.20.1/natives";

    [Fact]
    public void Extract_WritesEntries_SkippingManifestAndDirectories()
    {
        InMemoryFileSystem fs = new();
        fs.WriteFile(Jar, MakeZip(
            ("META-INF/MANIFEST.MF", "manifest"),
            ("lwjgl.dll", "dll"),
            ("sub/lib.so", "so"),
            ("emptydir/", string.Empty)));
        NativesExtractor extractor = new(fs);

        Result<int> result = extractor.Extract([Jar], Natives);

        Assert.True(result.IsSuccess, result.Code + ": " + result.Detail);
        Assert.Equal(2, result.Value);
        Assert.True(fs.FileExists(Natives + "/lwjgl.dll"));
        Assert.True(fs.FileExists(Natives + "/sub/lib.so"));
        Assert.False(fs.FileExists(Natives + "/META-INF/MANIFEST.MF"));
    }

    [Fact]
    public void Extract_RejectsPathTraversal()
    {
        InMemoryFileSystem fs = new();
        fs.WriteFile(Jar, MakeZip(("../evil.dll", "evil"), ("ok.dll", "ok")));
        NativesExtractor extractor = new(fs);

        Result<int> result = extractor.Extract([Jar], Natives);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);
        Assert.False(fs.FileExists("D:/mc/versions/1.20.1/evil.dll"));
        Assert.True(fs.FileExists(Natives + "/ok.dll"));
    }

    [Fact]
    public void Extract_MissingJar_IsSkipped()
    {
        InMemoryFileSystem fs = new();

        Result<int> result = new NativesExtractor(fs).Extract(["D:/missing.jar"], Natives);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void Extract_CustomExclusions_AreApplied()
    {
        InMemoryFileSystem fs = new();
        fs.WriteFile(Jar, MakeZip(("skip.me", "x"), ("keep.me", "y")));

        Result<int> result = new NativesExtractor(fs).Extract([Jar], Natives, ["skip.me"]);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);
        Assert.True(fs.FileExists(Natives + "/keep.me"));
    }

    private static byte[] MakeZip(params (string Name, string Content)[] entries)
    {
        using MemoryStream stream = new();
        using (ZipArchive archive = new(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, string content) in entries)
            {
                ZipArchiveEntry entry = archive.CreateEntry(name);
                using StreamWriter writer = new(entry.Open(), Encoding.UTF8);
                writer.Write(content);
            }
        }

        return stream.ToArray();
    }
}