using Microsoft.EntityFrameworkCore;
using TibbiNav.Domain.Employees;
using TibbiNav.Domain.Identity;
using TibbiNav.Infrastructure;

namespace TibbiNav.Application.Authorization;

/// <summary>
/// Раздел 65: Employee не хранит DepartmentId/ManagerEmployeeId напрямую — это
/// атрибуты текущей EmploymentRecord (Effective Dating, раздел 10), поэтому
/// Department/OwnEmployees/Self резолвятся через join по EmploymentRecords,
/// а не через generic <see cref="ScopeQueryExtensions.ApplyScope{T}"/>.
/// </summary>
public static class EmployeeScopeQueryExtensions
{
    public static IQueryable<Employee> ApplyEmployeeScope(this IQueryable<Employee> query, ScopeContext ctx, TibbiNavDbContext db)
    {
        return ctx.Scope switch
        {
            PermissionScope.Organization => query,

            PermissionScope.Clinic => ctx.ClinicIds.Count == 0
                ? query.Where(_ => false)
                : query.Where(e => e.ClinicId != null && ctx.ClinicIds.Contains(e.ClinicId.Value)),

            PermissionScope.Department => ctx.DepartmentIds.Count == 0
                ? query.Where(_ => false)
                : query.Where(e => db.EmploymentRecords.Any(r =>
                    r.EmployeeId == e.Id && r.IsCurrent && ctx.DepartmentIds.Contains(r.DepartmentId))),

            PermissionScope.OwnEmployees => ctx.EmployeeId is null
                ? query.Where(_ => false)
                : query.Where(e => db.EmploymentRecords.Any(r =>
                    r.EmployeeId == e.Id && r.IsCurrent && r.ManagerEmployeeId == ctx.EmployeeId)),

            PermissionScope.Self => ctx.EmployeeId is null
                ? query.Where(_ => false)
                : query.Where(e => e.Id == ctx.EmployeeId),

            _ => query.Where(_ => false),
        };
    }
}
