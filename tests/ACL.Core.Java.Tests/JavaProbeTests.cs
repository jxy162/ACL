using ACL.Core.Abstractions;
using ACL.Core.Domain;
using ACL.Core.Java;
using ACL.Testing;
using Xunit;

namespace ACL.Core.Java.Tests;

public class JavaProbeTests
{
    private const string JavaPath = "C:/Program Files/Java/jdk-17/bin/java.exe";

    private const string Settings64 = """
    Property settings:
        file.encoding = UTF-8
        java.home = C:\Program Files\Java\jdk-17
        java.version = 17.0.9
        sun.arch.data.model = 64
        sun.jnu.encoding = UTF-8

    openjdk version "17.0.9" 2023-10-17
    OpenJDK Runtime Environment Temurin-17.0.9+9 (build 17.0.9+9)
    OpenJDK 64-Bit Server VM Temurin-17.0.9+9 (build 17.0.9+9, mixed mode, sharing)
    """;

    [Fact]
    public async Task Probe_ReadsProperties()
    {
        InMemoryFileSystem fs = new();
        fs.WriteFile(JavaPath, "stub");
        StubProcessLauncher process = new StubProcessLauncher().OnOutput(JavaPath, Settings64);
        JavaProbe probe = new(process, fs);

        Result<JavaRuntime> result = await probe.ProbeAsync(JavaPath);

        Assert.True(result.IsSuccess, result.Code + ": " + result.Detail);
        Assert.Equal(new Version(17, 0, 9), result.Value!.Version);
        Assert.True(result.Value!.Is64Bit);
        Assert.Equal(@"C:\Program Files\Java\jdk-17", result.Value!.HomeFolder);
        Assert.Equal("UTF-8", result.Value!.FileEncoding);
        Assert.Contains("-XshowSettings:properties", process.Specs[0].Arguments);
    }

    [Fact]
    public async Task Probe_32Bit_IsNot64Bit()
    {
        InMemoryFileSystem fs = new();
        fs.WriteFile(JavaPath, "stub");
        string output = Settings64.Replace("sun.arch.data.model = 64", "sun.arch.data.model = 32", StringComparison.Ordinal);
        StubProcessLauncher process = new StubProcessLauncher().OnOutput(JavaPath, output);

        Result<JavaRuntime> result = await new JavaProbe(process, fs).ProbeAsync(JavaPath);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Is64Bit);
    }

    [Fact]
    public async Task Probe_MissingFile_ReturnsNotFound()
    {
        Result<JavaRuntime> result = await new JavaProbe(new StubProcessLauncher(), new InMemoryFileSystem())
            .ProbeAsync(JavaPath);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.NotFound, result.Code);
    }

    [Fact]
    public async Task Probe_UnparsableOutput_ReturnsJavaMissing()
    {
        InMemoryFileSystem fs = new();
        fs.WriteFile(JavaPath, "stub");
        StubProcessLauncher process = new StubProcessLauncher().OnOutput(JavaPath, "garbage output");

        Result<JavaRuntime> result = await new JavaProbe(process, fs).ProbeAsync(JavaPath);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.JavaMissing, result.Code);
    }

    [Fact]
    public async Task Probe_BannerOnly_StillParsesVersion()
    {
        InMemoryFileSystem fs = new();
        fs.WriteFile(JavaPath, "stub");
        StubProcessLauncher process = new StubProcessLauncher().OnOutput(JavaPath, "openjdk version \"21.0.2\" 2024-01-16");

        Result<JavaRuntime> result = await new JavaProbe(process, fs).ProbeAsync(JavaPath);

        Assert.True(result.IsSuccess, result.Code + ": " + result.Detail);
        Assert.Equal(new Version(21, 0, 2), result.Value!.Version);
    }
}