using ACL.Core.Abstractions;
using ACL.Core.Auth;
using ACL.Core.Domain;
using ACL.Testing;
using Xunit;

namespace ACL.Core.Auth.Tests;

public class AuthServiceTests
{
    private const string Uuid = "069a79f444e94726a5befca90e38aaf5";
    private const string DeviceCodeUrl = "https://stub/devicecode";
    private const string TokenUrl = "https://stub/token";
    private const string XblUrl = "https://stub/xbl";
    private const string XstsUrl = "https://stub/xsts";
    private const string McLoginUrl = "https://stub/mclogin";
    private const string ProfileUrl = "https://stub/profile";

    private const string DeviceCodeJson =
        """{ "device_code": "dc-1", "user_code": "ABCD-EFGH", "verification_uri": "https://microsoft.com/link", "expires_in": 900, "interval": 1 }""";
    private const string TokenJson = """{ "access_token": "msa-token", "refresh_token": "refresh-1", "expires_in": 3600 }""";
    private const string XblJson = """{ "Token": "xbl-token", "DisplayClaims": { "xui": [ { "uhs": "uhs-1" } ] } }""";
    private const string XstsJson = """{ "Token": "xsts-token", "DisplayClaims": { "xui": [ { "uhs": "uhs-1" } ] } }""";
    private const string McLoginJson = """{ "access_token": "mc-token", "expires_in": 86400 }""";
    private const string ProfileJson = """{ "id": "069a79f444e94726a5befca90e38aaf5", "name": "Notch" }""";

    [Fact]
    public async Task LoginOffline_ReturnsDeterministicSession()
    {
        AuthService service = new(new StubHttpTransport());

        Result<AuthSession> session = await service.LoginOfflineAsync("Steve");

        Assert.True(session.IsSuccess);
        Assert.Equal(AccountType.Offline, session.Value!.Type);
        Assert.Equal(OfflineIdentity.CreateUuid("Steve"), session.Value!.Uuid);
    }

    [Fact]
    public async Task LoginMicrosoft_CompletesFullChain()
    {
        (AuthService service, StubHttpTransport http) = CreateMicrosoftService();
        DeviceCodePromptInfo? shown = null;

        Result<AuthSession> session = await service.LoginMicrosoftAsync((info, _) =>
        {
            shown = info;
            return Task.CompletedTask;
        });

        Assert.True(session.IsSuccess, session.Code + ": " + session.Detail);
        Assert.Equal(AccountType.Microsoft, session.Value!.Type);
        Assert.Equal(Uuid, session.Value!.Uuid);
        Assert.Equal("Notch", session.Value!.Name);
        Assert.Equal("mc-token", session.Value!.AccessToken);
        Assert.Equal("refresh-1", session.Value!.RefreshToken);
        Assert.Equal("ABCD-EFGH", shown!.UserCode);
        Assert.Equal("Bearer mc-token", http.LastRequest!.Headers!["Authorization"]);
    }

    [Fact]
    public async Task LoginMicrosoft_PollsUntilAuthorized()
    {
        int tokenCalls = 0;
        StubHttpTransport http = new StubHttpTransport()
            .OnJson(DeviceCodeUrl, DeviceCodeJson)
            .OnAsync(TokenUrl, (_, _) =>
            {
                tokenCalls++;
                return Task.FromResult(tokenCalls == 1
                    ? StubResponses.Json("""{ "error": "authorization_pending" }""", 400)
                    : StubResponses.Json(TokenJson));
            })
            .OnJson(XblUrl, XblJson)
            .OnJson(XstsUrl, XstsJson)
            .OnJson(McLoginUrl, McLoginJson)
            .OnJson(ProfileUrl, ProfileJson);
        AuthService service = new(http, microsoft: Options());

        Result<AuthSession> session = await service.LoginMicrosoftAsync((_, _) => Task.CompletedTask);

        Assert.True(session.IsSuccess, session.Code + ": " + session.Detail);
        Assert.Equal(2, tokenCalls);
    }

    [Fact]
    public async Task LoginMicrosoft_MissingClientId_ReturnsInvalidAppRegistration()
    {
        StubHttpTransport http = new StubHttpTransport()
            .OnJson(DeviceCodeUrl, DeviceCodeJson);
        AuthService service = new(http, microsoft: Options(clientId: null));

        Result<AuthSession> session = await service.LoginMicrosoftAsync((_, _) => Task.CompletedTask);

        Assert.False(session.IsSuccess);
        Assert.Equal(ErrorCode.InvalidAppRegistration, session.Code);
    }

    [Fact]
    public async Task LoginMicrosoft_XstsDenied_ReturnsAuthDenied()
    {
        (AuthService service, _) = CreateMicrosoftService(http => http.OnJson(XstsUrl, """{ "XErr": 2148916233 }""", 401));

        Result<AuthSession> session = await service.LoginMicrosoftAsync((_, _) => Task.CompletedTask);

        Assert.False(session.IsSuccess);
        Assert.Equal(ErrorCode.AuthDenied, session.Code);
    }

    [Fact]
    public async Task LoginMicrosoft_ProfileNotFound_ReturnsNotFound()
    {
        (AuthService service, _) = CreateMicrosoftService(http =>
            http.On(XstsUrl, _ => StubResponses.Json(XstsJson))
                .OnJson(ProfileUrl, """{ "error": "NOT_FOUND" }""", 404));

        Result<AuthSession> session = await service.LoginMicrosoftAsync((_, _) => Task.CompletedTask);

        Assert.False(session.IsSuccess);
        Assert.Equal(ErrorCode.NotFound, session.Code);
    }

    [Fact]
    public async Task LoginThirdParty_ReturnsThirdPartySession()
    {
        const string yggdrasil = """{ "accessToken": "ygg-token", "selectedProfile": { "id": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "name": "Player" } }""";
        StubHttpTransport http = new StubHttpTransport().OnJson("https://littleskin.cn/api/yggdrasil/authserver/authenticate", yggdrasil);
        AuthService service = new(http);

        Result<AuthSession> session = await service.LoginThirdPartyAsync("https://littleskin.cn/api/yggdrasil", "user", "pass");

        Assert.True(session.IsSuccess, session.Code + ": " + session.Detail);
        Assert.Equal(AccountType.ThirdParty, session.Value!.Type);
        Assert.Equal("Player", session.Value!.Name);
        Assert.Equal("ygg-token", session.Value!.AccessToken);
    }

    [Fact]
    public async Task Refresh_ReplacesAccessTokenButKeepsIdentity()
    {
        StubHttpTransport http = new StubHttpTransport()
            .OnJson(TokenUrl, TokenJson)
            .OnJson(XblUrl, XblJson)
            .OnJson(XstsUrl, XstsJson)
            .OnJson(McLoginUrl, McLoginJson)
            .OnJson(ProfileUrl, ProfileJson);
        AuthService service = new(http, microsoft: Options());
        AuthSession expired = new(AccountType.Microsoft, Uuid, "Notch", "old-token", "refresh-1");

        Result<AuthSession> refreshed = await service.RefreshAsync(expired);

        Assert.True(refreshed.IsSuccess, refreshed.Code + ": " + refreshed.Detail);
        Assert.Equal("mc-token", refreshed.Value!.AccessToken);
        Assert.Equal(Uuid, refreshed.Value!.Uuid);
    }

    [Fact]
    public async Task Refresh_NonMicrosoft_ReturnsSameSession()
    {
        AuthService service = new(new StubHttpTransport());
        AuthSession offline = OfflineIdentity.CreateSession("Steve");

        Result<AuthSession> refreshed = await service.RefreshAsync(offline);

        Assert.True(refreshed.IsSuccess);
        Assert.Equal(offline, refreshed.Value);
    }

    private static MicrosoftAuthOptions Options(string? clientId = "test-client") => new(
        new Uri(DeviceCodeUrl),
        new Uri(TokenUrl),
        new Uri(XblUrl),
        new Uri(XstsUrl),
        new Uri(McLoginUrl),
        new Uri(ProfileUrl),
        ClientId: clientId);

    private static (AuthService Service, StubHttpTransport Http) CreateMicrosoftService(
        Action<StubHttpTransport>? customize = null)
    {
        StubHttpTransport http = new StubHttpTransport()
            .OnJson(DeviceCodeUrl, DeviceCodeJson)
            .OnJson(TokenUrl, TokenJson)
            .OnJson(XblUrl, XblJson)
            .OnJson(XstsUrl, XstsJson)
            .OnJson(McLoginUrl, McLoginJson)
            .OnJson(ProfileUrl, ProfileJson);
        customize?.Invoke(http);
        return (new AuthService(http, microsoft: Options()), http);
    }
}