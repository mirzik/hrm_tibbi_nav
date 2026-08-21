using System.Security.Claims;
using TibbiNav.Api.Authorization;
using TibbiNav.Application.Authorization;

namespace TibbiNav.Api.Middleware;

/// <summary>
/// Раздел 65 ТЗ: RBAC enforcement. Для каждого запроса к endpoint, размеченному
/// <see cref="RequirePermissionAttribute"/>, резолвит роли текущего
/// аутентифицированного пользователя и проверяет, что хотя бы одна из них даёт
/// нужное Permission (Resource + Action). Разрешённый scope (Organization/
/// Clinic/Department/OwnEmployees/Self + допустимые Clinic/Department Id +
/// Restricted Fields) кладётся в <see cref="IScopeContextAccessor"/> — дальше
/// контроллер применяет его к запросу через ApplyScope/ApplyEmployeeScope и
/// ToRestrictedDictionary (см. TibbiNav.Application.Authorization).
///
/// Без совпадающего Permission — 403. Без аутентификации — 401 (дублирует
/// [Authorize], но не полагается на порядок регистрации endpoint-ов).
/// Регистрируется в Program.cs после UseAuthentication/UseAuthorization,
/// до MapControllers.
/// </summary>
public sealed class ScopeFilterMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IScopeContextResolver resolver, IScopeContextAccessor accessor)
    {
        var required = context.GetEndpoint()?.Metadata.GetMetadata<RequirePermissionAttribute>();

        if (required is null)
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var email = context.User.FindFirstValue(ClaimTypes.Email)
            ?? context.User.FindFirstValue("email")
            ?? context.User.FindFirstValue("preferred_username");

        if (string.IsNullOrWhiteSpace(email))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var scope = await resolver.ResolveAsync(email, required.Resource, required.Action, context.RequestAborted);
        if (scope is null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "forbidden",
                message = $"У вас нет разрешения {required.Action} на {required.Resource}.",
            }, context.RequestAborted);
            return;
        }

        accessor.Current = scope;
        await next(context);
    }
}
