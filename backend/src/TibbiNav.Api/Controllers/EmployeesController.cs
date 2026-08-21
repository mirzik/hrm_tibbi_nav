using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TibbiNav.Domain.Employees;
using TibbiNav.Infrastructure;

namespace TibbiNav.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/employees")]
public class EmployeesController(TibbiNavDbContext db) : ControllerBase
{
    /// <summary>
    /// Список сотрудников. TODO: подключить ScopeFilter middleware (раздел 65),
    /// который автоматически сузит запрос до Clinic/Department текущего пользователя.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? clinicId, [FromQuery] string? search, CancellationToken ct)
    {
        var query = db.Employees.AsNoTracking().AsQueryable();

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

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var employee = await db.Employees
            .Include(e => e.EmploymentRecords.Where(r => r.IsCurrent))
            .Include(e => e.MedicalCredentials)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        return employee is null ? NotFound() : Ok(employee);
    }

    /// <summary>Раздел 71: Global Search — упрощённая версия по одному модулю.</summary>
    [HttpGet("expiring-credentials")]
    public async Task<IActionResult> ExpiringCredentials([FromQuery] int withinDays = 30, CancellationToken ct = default)
    {
        var threshold = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(withinDays));

        var result = await db.MedicalCredentials
            .AsNoTracking()
            .Where(c => c.ExpiryDate != null && c.ExpiryDate <= threshold && c.Status != CredentialStatus.Expired)
            .Select(c => new { c.EmployeeId, c.Title, c.ExpiryDate, c.Status })
            .ToListAsync(ct);

        return Ok(result);
    }
}
