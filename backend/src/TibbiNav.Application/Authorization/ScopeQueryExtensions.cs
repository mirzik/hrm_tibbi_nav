using TibbiNav.Domain.Common;
using TibbiNav.Domain.Identity;

namespace TibbiNav.Application.Authorization;

/// <summary>
/// Раздел 65: применяет Scope из ScopeContext к сущностям, привязанным к
/// оргструктуре напрямую через IOrganizationScoped (Vacancy, Position,
/// Department, Clinic...). Для Employee используйте
/// <see cref="EmployeeScopeQueryExtensions"/> — там Department/OwnEmployees/Self
/// резолвятся через EmploymentRecord (Effective Dating, раздел 10), а не через
/// собственное поле сущности.
/// </summary>
public static class ScopeQueryExtensions
{
    public static IQueryable<T> ApplyScope<T>(this IQueryable<T> query, ScopeContext ctx)
        where T : class, IOrganizationScoped
    {
        return ctx.Scope switch
        {
            PermissionScope.Organization => query,

            // Department/OwnEmployees/Self не имеют собственного смысла для сущностей
            // без прямой привязки к сотруднику (Vacancy, Position...) — трактуем как
            // Clinic: самый узкий уровень, для которого у нас вообще есть Id клиники.
            PermissionScope.Clinic or PermissionScope.Department
                or PermissionScope.OwnEmployees or PermissionScope.Self => ctx.ClinicIds.Count == 0
                ? query.Where(_ => false)
                : query.Where(e => e.ClinicId != null && ctx.ClinicIds.Contains(e.ClinicId.Value)),

            _ => query.Where(_ => false),
        };
    }
}
