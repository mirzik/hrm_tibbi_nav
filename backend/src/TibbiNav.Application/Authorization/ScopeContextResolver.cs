using Microsoft.EntityFrameworkCore;
using TibbiNav.Domain.Identity;
using TibbiNav.Infrastructure;

namespace TibbiNav.Application.Authorization;

public interface IScopeContextResolver
{
    /// <summary>Возвращает разрешённый ScopeContext, либо null, если ни одна роль
    /// пользователя не даёт Permission на Resource+Action (= 403 в middleware).</summary>
    Task<ScopeContext?> ResolveAsync(string userEmail, string resource, PermissionAction action, CancellationToken ct = default);
}

/// <summary>
/// Раздел 65 ТЗ: собирает эффективный Permission пользователя на Resource+Action
/// из всех его ролей (у пользователя может быть несколько UserRole — напр.
/// ChiefDoctor одновременно в двух клиниках, раздел 4).
/// </summary>
public sealed class ScopeContextResolver(TibbiNavDbContext db) : IScopeContextResolver
{
    // От самого широкого к самому узкому — см. PermissionScope (раздел 65).
    private static readonly PermissionScope[] ScopeWidestFirst =
    [
        PermissionScope.Organization,
        PermissionScope.Clinic,
        PermissionScope.Department,
        PermissionScope.OwnEmployees,
        PermissionScope.Self,
    ];

    public async Task<ScopeContext?> ResolveAsync(string userEmail, string resource, PermissionAction action, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == userEmail && u.IsActive, ct);
        if (user is null) return null;

        var assignments = await db.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => new { ur.RoleId, ur.ScopeClinicId, ur.ScopeDepartmentId })
            .ToListAsync(ct);
        if (assignments.Count == 0) return null;

        var roleIds = assignments.Select(a => a.RoleId).Distinct().ToList();

        var permissions = await db.RolePermissions.AsNoTracking()
            .Where(p => roleIds.Contains(p.RoleId) && p.Resource == resource && p.Action == action)
            .ToListAsync(ct);
        if (permissions.Count == 0) return null; // ни одна роль не даёт доступ

        // Берём самый широкий Scope среди permission, которые реально дают доступ.
        var bestScope = ScopeWidestFirst.First(s => permissions.Any(p => p.Scope == s));
        var grantingRoleIds = permissions.Select(p => p.RoleId).ToHashSet();

        var clinicIds = assignments
            .Where(a => grantingRoleIds.Contains(a.RoleId) && a.ScopeClinicId is not null)
            .Select(a => a.ScopeClinicId!.Value)
            .ToHashSet();

        var departmentIds = assignments
            .Where(a => grantingRoleIds.Contains(a.RoleId) && a.ScopeDepartmentId is not null)
            .Select(a => a.ScopeDepartmentId!.Value)
            .ToHashSet();

        // Консервативный выбор (раздел 66): поле остаётся скрытым, если хотя бы
        // одна из ролей, давших это Permission, его ограничивает — объединение,
        // а не пересечение RestrictedFields.
        var restrictedFields = permissions
            .SelectMany(p => (p.RestrictedFields ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new ScopeContext
        {
            UserId = user.Id,
            EmployeeId = user.EmployeeId,
            Resource = resource,
            Action = action,
            Scope = bestScope,
            ClinicIds = clinicIds,
            DepartmentIds = departmentIds,
            RestrictedFields = restrictedFields,
        };
    }
}
