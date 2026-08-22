using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TibbiNav.Api.Authorization;
using TibbiNav.Application.Authorization;
using TibbiNav.Application.PerformanceReviews;
using TibbiNav.Domain.Identity;
using TibbiNav.Domain.PerformanceReviews;
using TibbiNav.Infrastructure;

namespace TibbiNav.Api.Controllers;

public record CreateReviewCycleRequest(Guid OrganizationId, Guid? ClinicId, string Name, ReviewCycleType Type, DateOnly PeriodStart, DateOnly PeriodEnd);
public record AddParticipantRequest(Guid EmployeeId);
public record AddReviewerRequest(ReviewerRole ReviewerRole, Guid ReviewerId);

/// <summary>
/// Раздел 47 ТЗ: управление циклами Performance Review (Self/Manager/180°/360°)
/// и участниками — со стороны HR/руководителя. Сама подача оценки — через
/// self-service (MeController.SubmitReview), т.к. оценщиком выступает
/// произвольный сотрудник (self/manager/peer/subordinate), а не HR.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/performance-review-cycles")]
public class PerformanceReviewCyclesController(TibbiNavDbContext db, PerformanceReviewService reviewService, IScopeContextAccessor scopeAccessor) : ControllerBase
{
    [HttpGet]
    [RequirePermission("PerformanceReview", PermissionAction.View)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;
        var cycles = await db.ReviewCycles.AsNoTracking().ApplyScope(scope)
            .OrderByDescending(c => c.PeriodStart)
            .Select(c => new { c.Id, c.Name, c.Type, c.PeriodStart, c.PeriodEnd, c.Status })
            .ToListAsync(ct);
        return Ok(cycles);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("PerformanceReview", PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;
        var cycle = await db.ReviewCycles.AsNoTracking().ApplyScope(scope).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cycle is null) return NotFound();
        return Ok(new { cycle.Id, cycle.Name, cycle.Type, cycle.PeriodStart, cycle.PeriodEnd, cycle.Status });
    }

    [HttpPost]
    [RequirePermission("PerformanceReview", PermissionAction.Create)]
    public async Task<IActionResult> Create([FromBody] CreateReviewCycleRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { error = "Наименование обязательно." });
        if (req.PeriodEnd < req.PeriodStart) return BadRequest(new { error = "Дата окончания периода раньше даты начала." });

        var cycle = new ReviewCycle
        {
            OrganizationId = req.OrganizationId,
            ClinicId = req.ClinicId,
            Name = req.Name,
            Type = req.Type,
            PeriodStart = req.PeriodStart,
            PeriodEnd = req.PeriodEnd,
            Status = ReviewCycleStatus.Open,
        };
        db.ReviewCycles.Add(cycle);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = cycle.Id }, new { cycle.Id, cycle.Name, cycle.Type, cycle.PeriodStart, cycle.PeriodEnd, cycle.Status });
    }

    [HttpPost("{id:guid}/close")]
    [RequirePermission("PerformanceReview", PermissionAction.Edit)]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;
        var cycle = await db.ReviewCycles.ApplyScope(scope).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cycle is null) return NotFound();

        cycle.Status = ReviewCycleStatus.Closed;
        await db.SaveChangesAsync(ct);
        return Ok(new { cycle.Id, cycle.Status });
    }

    [HttpGet("{id:guid}/participants")]
    [RequirePermission("PerformanceReview", PermissionAction.View)]
    public async Task<IActionResult> ListParticipants(Guid id, CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;
        var participants = await db.ReviewParticipants.AsNoTracking().ApplyScope(scope)
            .Where(p => p.ReviewCycleId == id)
            .Include(p => p.Assignments)
            .ToListAsync(ct);

        return Ok(participants.Select(ToParticipantSummaryDto));
    }

    [HttpGet("participants/{participantId:guid}")]
    [RequirePermission("PerformanceReview", PermissionAction.View)]
    public async Task<IActionResult> GetParticipant(Guid participantId, CancellationToken ct)
    {
        var participant = await GetParticipantInScopeAsync(participantId, ct);
        return participant is null ? NotFound() : Ok(ToParticipantDetailDto(participant));
    }

    [HttpPost("{id:guid}/participants")]
    [RequirePermission("PerformanceReview", PermissionAction.Create)]
    public async Task<IActionResult> AddParticipant(Guid id, [FromBody] AddParticipantRequest req, CancellationToken ct)
    {
        try
        {
            var participant = await reviewService.AddParticipantAsync(id, req.EmployeeId, ct);
            await db.Entry(participant).Collection(p => p.Assignments).LoadAsync(ct);
            return CreatedAtAction(nameof(GetParticipant), new { participantId = participant.Id }, ToParticipantDetailDto(participant));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("participants/{participantId:guid}/reviewers")]
    [RequirePermission("PerformanceReview", PermissionAction.Create)]
    public async Task<IActionResult> AddReviewer(Guid participantId, [FromBody] AddReviewerRequest req, CancellationToken ct)
    {
        if (await GetParticipantInScopeAsync(participantId, ct) is null) return NotFound();

        try
        {
            var assignment = await reviewService.AddReviewerAsync(participantId, req.ReviewerRole, req.ReviewerId, ct);
            return Ok(new { assignment.Id, assignment.ReviewerRole, assignment.ReviewerId, assignment.Status });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>"Расчёт итоговой оценки" — требует, чтобы все назначенные
    /// ReviewAssignment участника были поданы (см. PerformanceReviewService).</summary>
    [HttpPost("participants/{participantId:guid}/calculate")]
    [RequirePermission("PerformanceReview", PermissionAction.Edit)]
    public async Task<IActionResult> CalculateOverallScore(Guid participantId, CancellationToken ct)
    {
        if (await GetParticipantInScopeAsync(participantId, ct) is null) return NotFound();

        try
        {
            var participant = await reviewService.CalculateOverallScoreAsync(participantId, ct);
            return Ok(new { participant.Id, participant.OverallScore, participant.OverallScoreCalculatedAtUtc, participant.Status });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    private async Task<ReviewParticipant?> GetParticipantInScopeAsync(Guid participantId, CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;
        return await db.ReviewParticipants.AsNoTracking().ApplyScope(scope)
            .Include(p => p.Assignments).ThenInclude(a => a.KpiReferences)
            .FirstOrDefaultAsync(p => p.Id == participantId, ct);
    }

    private static object ToParticipantSummaryDto(ReviewParticipant p) => new
    {
        p.Id,
        p.ReviewCycleId,
        p.EmployeeId,
        p.Status,
        p.OverallScore,
        Assignments = p.Assignments.Select(a => new { a.Id, a.ReviewerRole, a.ReviewerId, a.Status, a.Score }),
    };

    private static object ToParticipantDetailDto(ReviewParticipant p) => new
    {
        p.Id,
        p.ReviewCycleId,
        p.EmployeeId,
        p.Status,
        p.OverallScore,
        p.OverallScoreCalculatedAtUtc,
        Assignments = p.Assignments.Select(a => new
        {
            a.Id,
            a.ReviewerRole,
            a.ReviewerId,
            a.Status,
            a.Score,
            a.Strengths,
            a.AreasForImprovement,
            a.Comments,
            a.SubmittedAtUtc,
            KpiAssignmentIds = a.KpiReferences.Select(r => r.KpiAssignmentId),
        }),
    };
}
