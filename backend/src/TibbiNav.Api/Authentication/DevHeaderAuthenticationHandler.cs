using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace TibbiNav.Api.Authentication;

/// <summary>
/// Только для Development (раздел 79: в проде — реальный OIDC/Keycloak через
/// JwtBearer, см. Program.cs). Пока внешний Identity Provider не поднят
/// (roadmap-пункт 10 в README), позволяет аутентифицироваться заголовком
/// `X-Dev-User: email@...`, чтобы локально проверить ScopeFilterMiddleware/RBAC
/// без реального JWT. Никогда не регистрируется вне Development — см. Program.cs.
/// </summary>
public sealed class DevHeaderAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DevHeader";
    public const string HeaderName = "X-Dev-User";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var email) || string.IsNullOrWhiteSpace(email))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new[]
        {
            new Claim(ClaimTypes.Email, email!),
            new Claim(ClaimTypes.Name, email!),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
