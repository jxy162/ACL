using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Java;
using ACL.Testing;
using Xunit;

namespace ACL.Core.Java.Tests;

public class JavaLocatorTests
{
    private const string Root = "C:/Program Files/Java";

    [Fact]
    public async Task Locate_FindsNestedAndDirectBinJava()
    {
        InMemoryFileSystem fs = new();
        StubProcessLauncher process = new StubProcessLauncher();
        AddJdk(fs, process, "C:/Program Files/Java/jdk-17", 17, 0, 9);
        AddJdk(fs, process, "C:/Program Files/Java/jdk-8", 8, 0, 392);
        fs.WriteFile("C:/Program Files/Java/jdk-17/bin/java.exe", "stub");

        JavaLocator locator = new(fs, new JavaProbe(process, fs), [Root]);

        IReadOnlyList<JavaRuntime> runtimes = await locator.LocateAsync();

        Assert.Equal(2, runtimes.Count);
        Assert.Equal(new Version(17, 0, 9), runtimes[0].Version);   // 版本降序
        Assert.Equal(new Version(8, 0, 392), runtimes[1].Version);
    }

    [Fact]
    public async Task Locate_FiltersSpecialPaths()
    {
        InMemoryFileSystem fs = new();
        StubProcessLauncher process = new StubProcessLauncher();
        AddJdk(fs, process, "C:/Windows/System32", 17, 0, 9);
        AddJdk(fs, process, "C:/Program Files/Common Files/Oracle/Java/javapath_target_123", 17, 0, 9);
        AddJdk(fs, process, "C:/Java/jdk-17", 17, 0, 9);

        JavaLocator locator = new(fs, new JavaProbe(process, fs), ["C:/Windows", "C:/Program Files/Common Files", "C:/Java"]);

        IReadOnlyList<JavaRuntime> runtimes = await locator.LocateAsync();

        JavaRuntime single = Assert.Single(runtimes);
        Assert.Equal("C:\\Java\\jdk-17", single.HomeFolder);
    }

    [Fact]
    public async Task Locate_SkipsUnprobeableJava()
    {
        InMemoryFileSystem fs = new();
        StubProcessLauncher process = new StubProcessLauncher();
        AddJdk(fs, process, "C:/Java/broken", 17, 0, 9);
        process.OnMissing("C:/Java/broken/bin/java.exe");
        AddJdk(fs, process, "C:/Java/ok", 21, 0, 0);

        JavaLocator locator = new(fs, new JavaProbe(process, fs), ["C:/Java"]);

        IReadOnlyList<JavaRuntime> runtimes = await locator.LocateAsync();

        Assert.Single(runtimes);
        Assert.Equal(new Version(21, 0, 0), runtimes[0].Version);
    }

    private static void AddJdk(InMemoryFileSystem fs, StubProcessLauncher process, string home, int major, int minor, int build)
    {
        string exe = home + "/bin/java.exe";
        fs.WriteFile(exe, "stub");
        string homeWindows = home.Replace('/', '\\');
        string output = $"Property settings:\n    java.home = {homeWindows}\n    java.version = {major}.{minor}.{build}\n    sun.arch.data.model = 64\n\nopenjdk version \"{major}.{minor}.{build}\" 2024-01-01";
        process.OnOutput(exe, output);
    }
}