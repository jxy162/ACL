using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Auth;

/// <summary>设备码提示信息（面向宿主展示，不含 <c>device_code</c>）。</summary>
/// <param name="UserCode">用户码。</param>
/// <param name="VerificationUri">验证地址。</param>
/// <param name="VerificationUriComplete">带用户码的验证地址（可空）。</param>
/// <param name="ExpiresAt">设备码过期时间（UTC）。</param>
public sealed record DeviceCodePromptInfo(
    string UserCode,
    string VerificationUri,
    string? VerificationUriComplete,
    DateTimeOffset ExpiresAt);

/// <summary>微软认证端点（可覆盖，便于桩测试）。</summary>
/// <param name="DeviceCodeEndpoint">设备码端点。</param>
/// <param name="TokenEndpoint">令牌端点。</param>
/// <param name="XblEndpoint">Xbox Live 端点。</param>
/// <param name="XstsEndpoint">XSTS 端点。</param>
/// <param name="MinecraftLoginEndpoint">Minecraft 登录端点。</param>
/// <param name="MinecraftProfileEndpoint">Minecraft 档案端点。</param>
/// <param name="Scope">OAuth scope。</param>
/// <param name="ClientId">应用 Client ID（为空时经 <see cref="ISecretProvider"/> 获取）。</param>
public sealed record MicrosoftAuthOptions(
    Uri DeviceCodeEndpoint,
    Uri TokenEndpoint,
    Uri XblEndpoint,
    Uri XstsEndpoint,
    Uri MinecraftLoginEndpoint,
    Uri MinecraftProfileEndpoint,
    string Scope = "XboxLive.signin offline_access",
    string? ClientId = null)
{
    /// <summary>官方默认端点。</summary>
    public static MicrosoftAuthOptions Default { get; } = new(
        new Uri("https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode"),
        new Uri("https://login.microsoftonline.com/consumers/oauth2/v2.0/token"),
        new Uri("https://user.auth.xboxlive.com/user/authenticate"),
        new Uri("https://xsts.auth.xboxlive.com/xsts/authorize"),
        new Uri("https://api.minecraftservices.com/authentication/login_with_xbox"),
        new Uri("https://api.minecraftservices.com/minecraft/profile"));
}

/// <summary>认证服务：微软正版 / 离线 / 第三方。</summary>
public interface IAuthService
{
    /// <summary>离线登录（确定性 UUID）。</summary>
    Task<Result<AuthSession>> LoginOfflineAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// 微软设备码登录：先展示用户码（回调），再轮询完成 OAuth → XBL → XSTS → Minecraft → Profile 令牌链。
    /// </summary>
    /// <param name="onDeviceCode">展示设备码信息的回调（宿主负责展示给用户）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<Result<AuthSession>> LoginMicrosoftAsync(
        Func<DeviceCodePromptInfo, CancellationToken, Task> onDeviceCode,
        CancellationToken cancellationToken = default);

    /// <summary>第三方（Yggdrasil / authlib-injector）登录。</summary>
    /// <param name="apiRoot">Yggdrasil API 根地址。</param>
    /// <param name="username">用户名。</param>
    /// <param name="password">密码。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<Result<AuthSession>> LoginThirdPartyAsync(
        string apiRoot,
        string username,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>刷新会话（微软正版支持；其他类型原样返回）。</summary>
    Task<Result<AuthSession>> RefreshAsync(AuthSession session, CancellationToken cancellationToken = default);
}