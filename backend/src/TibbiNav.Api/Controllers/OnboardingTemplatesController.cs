using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TibbiNav.Api.Authorization;
using TibbiNav.Domain.Identity;
using TibbiNav.Domain.Onboarding;
using TibbiNav.Domain.Organization;
using TibbiNav.Infrastructure;

namespace TibbiNav.Api.Controllers;

public record CreateOnboardingTemplateTaskRequest(
    OnboardingStage Stage, string Title, string? Description, OnboardingResponsible Responsible, int? DueOffsetDays, int OrderIndex);

public record CreateOnboardingTemplateRequest(
    string Name, PersonnelCategory? PersonnelCategory, Guid? PositionId, Guid? ClinicId,
    List<CreateOnboardingTemplateTaskRequest> Tasks);

/// <summary>Раздел 25: управление шаблонами чеклиста адаптации — какие задачи и
/// на каком этапе, для какой категории персонала/должности/клиники.</summary>
[ApiController]
[Authorize]
[Route("api/v1/onboarding/templates")]
public class OnboardingTemplatesController(TibbiNavDbContext db) : ControllerBase
{
    [HttpGet]
    [RequirePermission("OnboardingTemplate", PermissionAction.View)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var templates = await db.OnboardingChecklistTemplates
            .AsNoTracking()
            .Include(t => t.Tasks)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

        return Ok(templates.Select(ToDto));
    }

    [HttpPost]
    [RequirePermission("OnboardingTemplate", PermissionAction.Create)]
    public async Task<IActionResult> Create([FromBody] CreateOnboardingTemplateRequest req, CancellationToken ct)
    {
        if (req.Tasks is null || req.Tasks.Count == 0)
            return BadRequest(new { error = "В шаблоне должна быть хотя бы одна задача." });

        var template = new OnboardingChecklistTemplate
        {
            Name = req.Name,
            PersonnelCategory = req.PersonnelCategory,
            PositionId = req.PositionId,
            ClinicId = req.ClinicId,
        };

        foreach (var t in req.Tasks)
        {
            template.Tasks.Add(new OnboardingChecklistTemplateTask
            {
                Template = template,
                Stage = t.Stage,
                Title = t.Title,
                Description = t.Description,
                Responsible = t.Responsible,
                DueOffsetDays = t.DueOffsetDays,
                OrderIndex = t.OrderIndex,
            });
        }

        db.OnboardingChecklistTemplates.Add(template);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(List), new { }, ToDto(template));
    }

    private static object ToDto(OnboardingChecklistTemplate t) => new
    {
        t.Id,
        t.Name,
        t.PersonnelCategory,
        t.PositionId,
        t.ClinicId,
        t.IsActive,
        Tasks = t.Tasks.OrderBy(x => x.OrderIndex).Select(x => new
        {
            x.Id,
            x.Stage,
            x.Title,
            x.Description,
            x.Responsible,
            x.DueOffsetDays,
            x.OrderIndex,
        }),
    };
}
