using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TibbiNav.Api.Authorization;
using TibbiNav.Application.Authorization;
using TibbiNav.Domain.Employees;
using TibbiNav.Domain.Identity;
using TibbiNav.Infrastructure;

namespace TibbiNav.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/employees")]
public class EmployeesController(TibbiNavDbContext db, IScopeContextAccessor scopeAccessor) : ControllerBase
{
    /// <summary>
    /// Список сотрудников. Раздел 65: ScopeFilterMiddleware уже проверил
    /// Permission(Employee, View) и разрешил доступ — здесь запрос сужается до
    /// Scope конкретного пользователя (Organization/Clinic/Department/
    /// OwnEmployees/Self) через ApplyEmployeeScope.
    /// </summary>
    [HttpGet]
    [RequirePermission("Employee", PermissionAction.View)]
    public async Task<IActionResult> List([FromQuery] Guid? clinicId, [FromQuery] string? search, CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;
        var query = db.Employees.AsNoTracking().ApplyEmployeeScope(scope, db);

        if (clinicId is not null)
            query = query.Where(e => e.ClinicId == clinicId);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(e => e.FullName.Contains(search) || e.EmployeeCode.Contains(search));

        var result = await query
            .OrderBy(e => e.FullName)
            .Select(e => new
            {
                e.Id,
                e.EmployeeCode,
                e.FullName,
                e.Status,
                e.ClinicId
            })
            .Take(200)
            .ToListAsync(ct);

        return Ok(result);
    }

    /// <summary>Раздел 66: RestrictedFields применяются здесь через
    /// ToRestrictedDictionary — чувствительные поля (Salary/BankAccount/
    /// NationalId и т.п.) вырезаются даже при разрешённом View. Записи
    /// EmploymentRecords дополнительно обогащаются читаемыми именами
    /// подразделения/должности/руководителя (сырые FK бесполезны в HR
    /// Workspace) — та же идея, что и в MeController.GetEmployment.</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission("Employee", PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;

        var employee = await db.Employees
            .ApplyEmployeeScope(scope, db)
            .Include(e => e.EmploymentRecords.OrderByDescending(r => r.EffectiveFrom))
            .Include(e => e.MedicalCredentials)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        // 404, а не 403: за пределами scope сотрудник просто "не существует" для
        // этого пользователя — не подтверждаем сам факт его наличия в системе.
        if (employee is null) return NotFound();

        var departmentIds = employee.EmploymentRecords.Select(r => r.DepartmentId).Distinct().ToList();
        var positionIds = employee.EmploymentRecords.Select(r => r.PositionId).Distinct().ToList();
        var managerIds = employee.EmploymentRecords.Select(r => r.ManagerEmployeeId).Where(i => i is not null).Distinct().ToList();

        var departments = await db.Departments.AsNoTracking().Where(d => departmentIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name, ct);
        var positions = await db.Positions.AsNoTracking().Where(p => positionIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Title, ct);
        var managers = await db.Employees.AsNoTracking().Where(e => managerIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FullName, ct);

        var employmentRecords = employee.EmploymentRecords.Select(r =>
        {
            var dict = r.ToRestrictedDictionary(scope.RestrictedFields);
            dict["DepartmentName"] = departments.GetValueOrDefault(r.DepartmentId);
            dict["PositionTitle"] = positions.GetValueOrDefault(r.PositionId);
            dict["ManagerFullName"] = r.ManagerEmployeeId is null ? null : managers.GetValueOrDefault(r.ManagerEmployeeId.Value);
            return dict;
        });

        return Ok(new
        {
            Employee = employee.ToRestrictedDictionary(scope.RestrictedFields),
            EmploymentRecords = employmentRecords,
            MedicalCredentials = employee.MedicalCredentials.Select(c => c.ToRestrictedDictionary(scope.RestrictedFields)),
        });
    }

    /// <summary>Раздел 71: Global Search — упрощённая версия по одному модулю.</summary>
    [HttpGet("expiring-credentials")]
    [RequirePermission("Employee", PermissionAction.View)]
    public async Task<IActionResult> ExpiringCredentials([FromQuery] int withinDays = 30, CancellationToken ct = default)
    {
        var scope = scopeAccessor.Current!;
        var threshold = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(withinDays));

        var scopedEmployeeIds = db.Employees.AsNoTracking().ApplyEmployeeScope(scope, db).Select(e => e.Id);

        var result = await db.MedicalCredentials
            .AsNoTracking()
            .Where(c => scopedEmployeeIds.Contains(c.EmployeeId))
            .Where(c => c.ExpiryDate != null && c.ExpiryDate <= threshold && c.Status != CredentialStatus.Expired)
            .Select(c => new { c.EmployeeId, c.Title, c.ExpiryDate, c.Status })
            .ToListAsync(ct);

        return Ok(result);
    }
}
