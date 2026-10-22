using ACL.Core.Abstractions;
using ACL.Core.Auth;
using ACL.Core.Download;
using ACL.Core.Java;
using ACL.Core.Launch;
using ACL.Core.Modpack;
using ACL.Core.Resources;
using ACL.Core.Version;

namespace ACL.Cli;

/// <summary>CLI 依赖集合（便于测试注入桩）。</summary>
public sealed record CliServices(
    IVersionCatalog Versions,
    IJavaLocator JavaLocator,
    IDownloadService Download,
    IHttpTransport Http,
    ILaunchPlanner LaunchPlanner,
    INativesExtractor Natives,
    IGameMonitor Monitor,
    IModpackInstaller Modpack,
    IAuthService Auth,
    IResourceIndex Modrinth,
    IResourceIndex CurseForge,
    string GameDirectory);