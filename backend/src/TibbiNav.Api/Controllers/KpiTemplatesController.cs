using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TibbiNav.Api.Authorization;
using TibbiNav.Application.Authorization;
using TibbiNav.Domain.Identity;
using TibbiNav.Domain.Kpi;
using TibbiNav.Infrastructure;

namespace TibbiNav.Api.Controllers;

public record CreateKpiTemplateRequest(
    Guid OrganizationId, Guid? ClinicId, string Name, string? Description,
    KpiLevel Level, KpiPeriodType PeriodType, decimal DefaultWeight,
    string Unit, string? Source, string? Formula);

/// <summary>
/// Раздел 45-46 ТЗ: управление переиспользуемыми определениями KPI
/// (корпоративные/клиника/подразделение/индивидуальные показатели). Назначение
/// на конкретного исполнителя и проставление факта — KpiAssignmentsController.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/kpi-templates")]
public class KpiTemplatesController(TibbiNavDbContext db, IScopeContextAccessor scopeAccessor) : ControllerBase
{
    [HttpGet]
    [RequirePermission("KpiTemplate", PermissionAction.View)]
    public async Task<IActionResult> List([FromQuery] KpiLevel? level, [FromQuery] bool? isActive, CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;
        var query = db.KpiTemplates.AsNoTracking().ApplyScope(scope);

        if (level is not null) query = query.Where(t => t.Level == level);
        if (isActive is not null) query = query.Where(t => t.IsActive == isActive);

        var templates = await query.OrderBy(t => t.Level).ThenBy(t => t.Name).ToListAsync(ct);
        return Ok(templates.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("KpiTemplate", PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;
        var template = await db.KpiTemplates.AsNoTracking().ApplyScope(scope).FirstOrDefaultAsync(t => t.Id == id, ct);
        return template is null ? NotFound() : Ok(ToDto(template));
    }

    [HttpPost]
    [RequirePermission("KpiTemplate", PermissionAction.Create)]
    public async Task<IActionResult> Create([FromBody] CreateKpiTemplateRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { error = "Наименование обязательно." });
        if (string.IsNullOrWhiteSpace(req.Unit)) return BadRequest(new { error = "Единица измерения обязательна." });
        if (req.DefaultWeight < 0 || req.DefaultWeight > 100) return BadRequest(new { error = "Вес должен быть в диапазоне 0-100." });

        var template = new KpiTemplate
        {
            OrganizationId = req.OrganizationId,
            ClinicId = req.ClinicId,
            Name = req.Name,
            Description = req.Description,
            Level = req.Level,
            PeriodType = req.PeriodType,
            DefaultWeight = req.DefaultWeight,
            Unit = req.Unit,
            Source = req.Source,
            Formula = req.Formula,
        };

        db.KpiTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = template.Id }, ToDto(template));
    }

    /// <summary>Мягкое отключение шаблона — не удаляет уже существующие
    /// назначения, просто запрещает новые (см. KpiAssignmentService.AssignAsync).</summary>
    [HttpPost("{id:guid}/deactivate")]
    [RequirePermission("KpiTemplate", PermissionAction.Edit)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;
        var template = await db.KpiTemplates.ApplyScope(scope).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return NotFound();

        template.IsActive = false;
        await db.SaveChangesAsync(ct);
        return Ok(ToDto(template));
    }

    private static object ToDto(KpiTemplate t) => new
    {
        t.Id,
        t.OrganizationId,
        t.ClinicId,
        t.Name,
        t.Description,
        t.Level,
        t.PeriodType,
        t.DefaultWeight,
        t.Unit,
        t.Source,
        t.Formula,
        t.IsActive,
    };
}
