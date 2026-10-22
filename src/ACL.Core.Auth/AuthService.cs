using System.Text.Json;
using ACL.Core.Abstractions;
using ACL.Core.Domain;

namespace ACL.Core.Auth;

/// <summary>
/// <see cref="IAuthService"/> 的默认实现：离线（确定性 UUID）、微软设备码令牌链、第三方 Yggdrasil。
/// </summary>
public sealed class AuthService : IAuthService
{
    /// <summary>微软应用 Client ID 的密钥键名。</summary>
    public const string MicrosoftClientIdSecretKey = "ACL_MS_CLIENT_ID";

    private readonly IHttpTransport _http;
    private readonly ISecretProvider _secrets;
    private readonly IAppLogger _logger;
    private readonly MicrosoftAuthOptions _microsoft;

    /// <summary>构造认证服务。</summary>
    /// <param name="http">HTTP 传输。</param>
    /// <param name="secrets">密钥提供方（读取 Client ID）。</param>
    /// <param name="microsoft">微软端点 / Client ID 覆盖。</param>
    /// <param name="logger">日志。</param>
    public AuthService(
        IHttpTransport http,
        ISecretProvider? secrets = null,
        MicrosoftAuthOptions? microsoft = null,
        IAppLogger? logger = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _secrets = secrets ?? EmptySecretProvider.Instance;
        _microsoft = microsoft ?? MicrosoftAuthOptions.Default;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc />
    public Task<Result<AuthSession>> LoginOfflineAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Task.FromResult(Result<AuthSession>.Failure(ErrorCode.AuthDenied, "用户名为空。"));
        }

        _logger.Information($"离线登录：{Redact(name)}");
        return Task.FromResult(Result<AuthSession>.Success(OfflineIdentity.CreateSession(name)));
    }

    /// <inheritdoc />
    public async Task<Result<AuthSession>> LoginMicrosoftAsync(
        Func<DeviceCodePromptInfo, CancellationToken, Task> onDeviceCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onDeviceCode);

        string? clientId = ResolveClientId();
        if (string.IsNullOrEmpty(clientId))
        {
            return Result<AuthSession>.Failure(ErrorCode.InvalidAppRegistration, "缺少微软应用 Client ID。");
        }

        Result<HttpText> device = await PostFormAsync(
            _microsoft.DeviceCodeEndpoint,
            new Dictionary<string, string> { ["client_id"] = clientId, ["scope"] = _microsoft.Scope },
            cancellationToken).ConfigureAwait(false);
        if (!device.IsSuccess)
        {
            return Result<AuthSession>.Failure(device.Code, device.Detail);
        }

        DeviceCodeResponse? code = ParseDeviceCode(device.Value!.Body);
        if (code is null)
        {
            return Result<AuthSession>.Failure(ErrorCode.AuthDenied, "设备码响应无效。");
        }

        DeviceCodePromptInfo prompt = new(
            code.UserCode,
            code.VerificationUri,
            code.VerificationUriComplete,
            DateTimeOffset.UtcNow.AddSeconds(code.ExpiresIn));
        await onDeviceCode(prompt, cancellationToken).ConfigureAwait(false);

        TimeSpan interval = TimeSpan.FromSeconds(Math.Max(1, code.Interval));
        DateTimeOffset deadline = prompt.ExpiresAt;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (DateTimeOffset.UtcNow >= deadline)
            {
                return Result<AuthSession>.Failure(ErrorCode.AuthExpired, "设备码已过期。");
            }

            Result<HttpText> token = await PostFormAsync(
                _microsoft.TokenEndpoint,
                new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                    ["device_code"] = code.DeviceCode,
                },
                cancellationToken).ConfigureAwait(false);
            if (!token.IsSuccess)
            {
                return Result<AuthSession>.Failure(token.Code, token.Detail);
            }

            HttpText text = token.Value!;
            if (text.StatusCode is >= 200 and < 300)
            {
                MsToken? ms = ParseToken(text.Body);
                return ms is null
                    ? Result<AuthSession>.Failure(ErrorCode.AuthDenied, "令牌响应无效。")
                    : await CompleteChainAsync(ms.AccessToken, ms.RefreshToken, ms.ExpiresIn, cancellationToken).ConfigureAwait(false);
            }

            switch (ParseError(text.Body))
            {
                case "authorization_pending":
                    break;
                case "slow_down":
                    interval += TimeSpan.FromSeconds(5);
                    break;
                case "expired_token":
                    return Result<AuthSession>.Failure(ErrorCode.AuthExpired, "设备码已过期。");
                case "authorization_declined":
                    return Result<AuthSession>.Failure(ErrorCode.AuthDenied, "用户取消了授权。");
                default:
                    return Result<AuthSession>.Failure(ErrorCode.AuthDenied, $"设备码轮询失败（HTTP {text.StatusCode}）。");
            }

            await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<Result<AuthSession>> LoginThirdPartyAsync(
        string apiRoot,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiRoot) || string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            return Result<AuthSession>.Failure(ErrorCode.AuthDenied, "第三方登录参数不完整。");
        }

        string endpoint = apiRoot.TrimEnd('/') + "/authserver/authenticate";
        string body = JsonSerializer.Serialize(new
        {
            username,
            password,
            clientToken = Guid.NewGuid().ToString("N"),
            agent = new { name = "Minecraft", version = 1 },
            requestUser = true,
        });

        Result<HttpText> result = await PostJsonAsync(new Uri(endpoint), body, null, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return Result<AuthSession>.Failure(result.Code, result.Detail);
        }

        if (result.Value!.StatusCode is < 200 or >= 300)
        {
            return Result<AuthSession>.Failure(ErrorCode.AuthDenied, $"第三方认证失败（HTTP {result.Value!.StatusCode}）。");
        }

        using JsonDocument document = JsonDocument.Parse(result.Value!.Body);
        string? accessToken = GetString(document.RootElement, "accessToken");
        if (accessToken is null
            || !document.RootElement.TryGetProperty("selectedProfile", out JsonElement profile)
            || profile.ValueKind != JsonValueKind.Object)
        {
            return Result<AuthSession>.Failure(ErrorCode.AuthDenied, "第三方认证响应无效。");
        }

        string? uuid = GetString(profile, "id");
        string? name = GetString(profile, "name");
        if (uuid is null || name is null)
        {
            return Result<AuthSession>.Failure(ErrorCode.AuthDenied, "第三方档案无效。");
        }

        return Result<AuthSession>.Success(new AuthSession(AccountType.ThirdParty, uuid, name, accessToken));
    }

    /// <inheritdoc />
    public async Task<Result<AuthSession>> RefreshAsync(AuthSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.Type != AccountType.Microsoft || string.IsNullOrEmpty(session.RefreshToken))
        {
            return Result<AuthSession>.Success(session);
        }

        string? clientId = ResolveClientId();
        if (string.IsNullOrEmpty(clientId))
        {
            return Result<AuthSession>.Failure(ErrorCode.InvalidAppRegistration, "缺少微软应用 Client ID。");
        }

        Result<HttpText> token = await PostFormAsync(
            _microsoft.TokenEndpoint,
            new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = session.RefreshToken,
                ["scope"] = _microsoft.Scope,
            },
            cancellationToken).ConfigureAwait(false);
        if (!token.IsSuccess)
        {
            return Result<AuthSession>.Failure(token.Code, token.Detail);
        }

        if (token.Value!.StatusCode is < 200 or >= 300)
        {
            return Result<AuthSession>.Failure(ErrorCode.AuthExpired, $"刷新令牌失败（HTTP {token.Value!.StatusCode}）。");
        }

        MsToken? ms = ParseToken(token.Value!.Body);
        return ms is null
            ? Result<AuthSession>.Failure(ErrorCode.AuthExpired, "刷新响应无效。")
            : await CompleteChainAsync(ms.AccessToken, ms.RefreshToken ?? session.RefreshToken, ms.ExpiresIn, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<AuthSession>> CompleteChainAsync(
        string msaAccessToken,
        string? refreshToken,
        int? expiresIn,
        CancellationToken cancellationToken)
    {
        string xblBody = JsonSerializer.Serialize(new
        {
            Properties = new
            {
                AuthMethod = "RPS",
                SiteName = "user.auth.xboxlive.com",
                RpsTicket = "d=" + msaAccessToken,
            },
            RelyingParty = "http://auth.xboxlive.com",
            TokenType = "JWT",
        });

        Result<HttpText> xbl = await PostJsonAsync(_microsoft.XblEndpoint, xblBody, null, cancellationToken).ConfigureAwait(false);
        if (!xbl.IsSuccess)
        {
            return Result<AuthSession>.Failure(xbl.Code, xbl.Detail);
        }

        (string? xblToken, string? uhs) = ParseXboxToken(xbl.Value!.Body);
        if (xbl.Value!.StatusCode is < 200 or >= 300 || xblToken is null || uhs is null)
        {
            return Result<AuthSession>.Failure(ErrorCode.AuthDenied, $"Xbox Live 认证失败（HTTP {xbl.Value!.StatusCode}）。");
        }

        string xstsBody = JsonSerializer.Serialize(new
        {
            Properties = new { SandboxId = "RETAIL", UserTokens = new[] { xblToken } },
            RelyingParty = "rp://api.minecraftservices.com/",
            TokenType = "JWT",
        });

        Result<HttpText> xsts = await PostJsonAsync(_microsoft.XstsEndpoint, xstsBody, null, cancellationToken).ConfigureAwait(false);
        if (!xsts.IsSuccess)
        {
            return Result<AuthSession>.Failure(xsts.Code, xsts.Detail);
        }

        (string? xstsToken, string? xstsUhs) = ParseXboxToken(xsts.Value!.Body);
        if (xsts.Value!.StatusCode is < 200 or >= 300 || xstsToken is null)
        {
            return Result<AuthSession>.Failure(ErrorCode.AuthDenied, $"XSTS 认证失败（HTTP {xsts.Value!.StatusCode}）。");
        }

        string userHash = xstsUhs ?? uhs;

        string mcBody = JsonSerializer.Serialize(new { identityToken = $"XBL3.0 x={userHash};{xstsToken}" });
        Result<HttpText> mc = await PostJsonAsync(_microsoft.MinecraftLoginEndpoint, mcBody, null, cancellationToken).ConfigureAwait(false);
        if (!mc.IsSuccess)
        {
            return Result<AuthSession>.Failure(mc.Code, mc.Detail);
        }

        if (mc.Value!.StatusCode is < 200 or >= 300)
        {
            ErrorCode code = mc.Value!.Body.Contains("Invalid app registration", StringComparison.OrdinalIgnoreCase)
                ? ErrorCode.InvalidAppRegistration
                : ErrorCode.AuthDenied;
            return Result<AuthSession>.Failure(code, $"Minecraft 登录失败（HTTP {mc.Value!.StatusCode}）。");
        }

        string? mcToken = ParseJsonString(mc.Value!.Body, "access_token");
        if (string.IsNullOrEmpty(mcToken))
        {
            return Result<AuthSession>.Failure(ErrorCode.AuthDenied, "Minecraft 令牌响应无效。");
        }

        Result<HttpText> profile = await GetAsync(
            _microsoft.MinecraftProfileEndpoint,
            new Dictionary<string, string> { ["Authorization"] = "Bearer " + mcToken },
            cancellationToken).ConfigureAwait(false);
        if (!profile.IsSuccess)
        {
            return Result<AuthSession>.Failure(profile.Code, profile.Detail);
        }

        if (profile.Value!.StatusCode == 404)
        {
            return Result<AuthSession>.Failure(ErrorCode.NotFound, "该账号未拥有 Minecraft。");
        }

        if (profile.Value!.StatusCode is < 200 or >= 300)
        {
            return Result<AuthSession>.Failure(ErrorCode.AuthDenied, $"读取游戏档案失败（HTTP {profile.Value!.StatusCode}）。");
        }

        string? uuid = ParseJsonString(profile.Value!.Body, "id");
        string? name = ParseJsonString(profile.Value!.Body, "name");
        if (string.IsNullOrEmpty(uuid) || string.IsNullOrEmpty(name))
        {
            return Result<AuthSession>.Failure(ErrorCode.AuthDenied, "游戏档案响应无效。");
        }

        DateTimeOffset? expiresAt = expiresIn is int seconds ? DateTimeOffset.UtcNow.AddSeconds(seconds) : null;
        _logger.Information($"微软登录成功：{Redact(name)}");
        return Result<AuthSession>.Success(new AuthSession(AccountType.Microsoft, uuid, name, mcToken, refreshToken, expiresAt));
    }

    private string? ResolveClientId() => _microsoft.ClientId ?? _secrets.GetSecret(MicrosoftClientIdSecretKey);

    private async Task<Result<HttpText>> PostFormAsync(Uri uri, IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken)
        => await SendAsync(new HttpRequest(uri, HttpVerb.Post, Content: HttpContentSpec.Form(fields)), cancellationToken).ConfigureAwait(false);

    private async Task<Result<HttpText>> PostJsonAsync(Uri uri, string json, IReadOnlyDictionary<string, string>? headers, CancellationToken cancellationToken)
        => await SendAsync(new HttpRequest(uri, HttpVerb.Post, headers, null, HttpContentSpec.Json(json)), cancellationToken).ConfigureAwait(false);

    private async Task<Result<HttpText>> GetAsync(Uri uri, IReadOnlyDictionary<string, string>? headers, CancellationToken cancellationToken)
        => await SendAsync(new HttpRequest(uri, HttpVerb.Get, headers), cancellationToken).ConfigureAwait(false);

    private async Task<Result<HttpText>> SendAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        Result<HttpResponse> sent = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!sent.IsSuccess)
        {
            return Result<HttpText>.Failure(sent.Code, sent.Detail);
        }

        using HttpResponse response = sent.Value!;
        string body = await response.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return Result<HttpText>.Success(new HttpText(response.StatusCode, body));
    }

    private static DeviceCodeResponse? ParseDeviceCode(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        string? deviceCode = GetString(root, "device_code");
        string? userCode = GetString(root, "user_code");
        string? verificationUri = GetString(root, "verification_uri");
        if (deviceCode is null || userCode is null || verificationUri is null)
        {
            return null;
        }

        int expiresIn = GetInt(root, "expires_in") ?? 900;
        int interval = GetInt(root, "interval") ?? 5;
        return new DeviceCodeResponse(deviceCode, userCode, verificationUri, GetString(root, "verification_uri_complete"), expiresIn, interval);
    }

    private static MsToken? ParseToken(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        string? accessToken = GetString(root, "access_token");
        return accessToken is null ? null : new MsToken(accessToken, GetString(root, "refresh_token"), GetInt(root, "expires_in"));
    }

    private static string? ParseError(string json)
    {
        try
        {
            return ParseJsonString(json, "error");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static (string? Token, string? UserHash) ParseXboxToken(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            string? token = GetString(root, "Token");
            string? uhs = null;
            if (root.TryGetProperty("DisplayClaims", out JsonElement claims)
                && claims.TryGetProperty("xui", out JsonElement xui)
                && xui.ValueKind == JsonValueKind.Array
                && xui.GetArrayLength() > 0)
            {
                uhs = GetString(xui[0], "uhs");
            }

            return (token, uhs);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static string? ParseJsonString(string json, string property)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return GetString(document.RootElement, property);
    }

    private static string? GetString(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? GetInt(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out JsonElement value)
            && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;

    private static string Redact(string value)
        => value.Length <= 2 ? "**" : value[0] + new string('*', Math.Max(1, value.Length - 2)) + value[^1];

    private readonly record struct HttpText(int StatusCode, string Body);

    private sealed record DeviceCodeResponse(
        string DeviceCode,
        string UserCode,
        string VerificationUri,
        string? VerificationUriComplete,
        int ExpiresIn,
        int Interval);

    private sealed record MsToken(string AccessToken, string? RefreshToken, int? ExpiresIn);

    private sealed class EmptySecretProvider : ISecretProvider
    {
        public static EmptySecretProvider Instance { get; } = new();

        public string? GetSecret(string key) => null;
    }

    private sealed class NullLogger : IAppLogger
    {
        public static NullLogger Instance { get; } = new();

        public bool IsEnabled(LogLevel level) => false;

        public void Log(LogLevel level, string message, Exception? exception = null)
        {
            // 有意丢弃。
        }
    }
}