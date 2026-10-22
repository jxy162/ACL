using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Version;
using ACL.Testing;
using Xunit;

namespace ACL.Core.Version.Tests;

public class InstalledInstanceScannerTests
{
    private const string Root = "C:/game";
    private static readonly string Versions = Root + "/versions";

    [Fact]
    public void Scan_DetectsFabricInstances()
    {
        InMemoryFileSystem fs = new InMemoryFileSystem();
        fs.WriteFile($"{Versions}/fabric-1.20.1/fabric-1.20.1.json", """
        {
          "id": "fabric-1.20.1",
          "inheritsFrom": "1.20.1",
          "mainClass": "net.fabricmc.loader.impl.launch.knot.KnotClient",
          "libraries": [ { "name": "net.fabricmc:fabric-loader:0.15.11" } ]
        }
        """);
        fs.CreateDirectory($"{Versions}/fabric-1.20.1");

        IReadOnlyList<InstalledInstance> instances = new InstalledInstanceScanner(fs).Scan(Versions);

        InstalledInstance instance = Assert.Single(instances);
        Assert.Equal("fabric-1.20.1", instance.Id);
        Assert.Equal("1.20.1", instance.InheritsFrom);
        Assert.Equal(ModLoaderKind.Fabric, instance.Loader);
        Assert.Equal("0.15.11", instance.LoaderVersion);
        Assert.False(instance.HasClientJar);
    }

    [Fact]
    public void Scan_IgnoresNestedDirectories()
    {
        InMemoryFileSystem fs = new InMemoryFileSystem();
        fs.WriteFile($"{Versions}/1.20.1/1.20.1.json", """{ "id": "1.20.1", "libraries": [] }""");
        fs.WriteFile($"{Versions}/1.20.1/1.20.1-natives/x.txt", "x");
        fs.WriteFile($"{Versions}/26.1-snapshot-1/26.1-snapshot-1.json", """{ "id": "26.1-snapshot-1", "libraries": [] }""");

        IReadOnlyList<InstalledInstance> instances = new InstalledInstanceScanner(fs).Scan(Versions);

        Assert.Equal(2, instances.Count);
        Assert.All(instances, instance => Assert.Equal(ModLoaderKind.None, instance.Loader));
        Assert.All(instances, instance => Assert.Equal("原版", instance.LoaderLabel));
    }

    [Fact]
    public void Scan_DetectsForgeAndOptiFine()
    {
        InMemoryFileSystem fs = new InMemoryFileSystem();
        fs.WriteFile($"{Versions}/1.20.1-forge-47.2.0/1.20.1-forge-47.2.0.json", """
        { "id": "1.20.1-forge-47.2.0", "libraries": [ { "name": "net.minecraftforge:forge:1.20.1-47.2.0" } ] }
        """);
        fs.WriteFile($"{Versions}/1.20.1-OptiFine/1.20.1-OptiFine.json", """
        { "id": "1.20.1-OptiFine", "libraries": [ { "name": "optifine:OptiFine:1.20.1_HD_U_I6" } ] }
        """);

        IReadOnlyList<InstalledInstance> instances = new InstalledInstanceScanner(fs).Scan(Versions);

        Assert.Contains(instances, i => i.Loader == ModLoaderKind.Forge && i.LoaderVersion == "1.20.1-47.2.0");
        Assert.Contains(instances, i => i.Loader == ModLoaderKind.OptiFine && i.LoaderVersion == "1.20.1_HD_U_I6");
    }

    [Fact]
    public void Scan_MissingDirectory_ReturnsEmpty()
    {
        IReadOnlyList<InstalledInstance> instances = new InstalledInstanceScanner(new InMemoryFileSystem()).Scan(Versions);

        Assert.Empty(instances);
    }
}