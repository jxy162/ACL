using ACL.Cli;
using ACL.Core.Abstractions;
using ACL.Core.Auth;
using ACL.Core.Download;
using ACL.Core.Java;
using ACL.Core.Launch;
using ACL.Core.Modpack;
using ACL.Core.Resources;
using ACL.Core.Version;
using ACL.Infrastructure;

// ACL 组合根：在此装配实现（内核不解析容器，见 ADR-0004）。
string gameDirectory = Environment.GetEnvironmentVariable("ACL_GAME_DIR") is { Length: > 0 } custom
    ? custom
    : Path.Combine(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), ".minecraft");

using HttpTransport http = new();
PhysicalFileSystem fileSystem = new();
MirrorPolicy mirrorPolicy = new();
DownloadService downloadService = new(http, fileSystem, null, null, null, DownloadThrottle.Unlimited);
VersionCatalog versionCatalog = new(http, mirrorPolicy);
ProcessLauncher processLauncher = new();
JavaProbe javaProbe = new(processLauncher, fileSystem);

List<string> javaRoots =
[
    Environment.GetEnvironmentVariable("JAVA_HOME") ?? string.Empty,
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Java"),
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Eclipse Adoptium"),
];
JavaLocator javaLocator = new(fileSystem, javaProbe, javaRoots);

ISecretProvider secrets = new CompositeSecretProvider(
    new FileSecretProvider(fileSystem, Path.Combine(AppContext.BaseDirectory, "ACL.secrets.json")),
    new EnvironmentSecretProvider());
AuthService authService = new(http, secrets);
LaunchPlanner launchPlanner = new();
NativesExtractor nativesExtractor = new(fileSystem);
GameMonitor gameMonitor = new(new GameProcessLauncher());
ModpackInstaller modpackInstaller = new(fileSystem, downloadService);

ModrinthClient modrinth = new(http);
CurseForgeClient curseForge = new(http, secrets);

CliServices services = new(
    versionCatalog,
    javaLocator,
    downloadService,
    http,
    launchPlanner,
    nativesExtractor,
    gameMonitor,
    modpackInstaller,
    authService,
    modrinth,
    curseForge,
    gameDirectory);

CliCommands commands = new(services, Console.Out);
using CancellationTokenSource cancellation = new();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

return await commands.RunAsync(args, cancellation.Token);