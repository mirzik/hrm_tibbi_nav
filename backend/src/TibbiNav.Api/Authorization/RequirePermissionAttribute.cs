using TibbiNav.Domain.Identity;

namespace TibbiNav.Api.Authorization;

/// <summary>
/// Раздел 65: размечает controller action требуемым Resource+Action.
/// Считывается <see cref="TibbiNav.Api.Middleware.ScopeFilterMiddleware"/> из
/// endpoint metadata — если атрибута нет, middleware пропускает запрос без
/// RBAC-проверки (напр. /health, Swagger, статика).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequirePermissionAttribute(string resource, PermissionAction action) : Attribute
{
    public string Resource { get; } = resource;
    public PermissionAction Action { get; } = action;
}
