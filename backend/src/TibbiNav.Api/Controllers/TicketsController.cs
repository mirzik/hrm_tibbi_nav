using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TibbiNav.Api.Authorization;
using TibbiNav.Application.Authorization;
using TibbiNav.Application.ServiceDesk;
using TibbiNav.Domain.Identity;
using TibbiNav.Domain.ServiceDesk;
using TibbiNav.Infrastructure;

namespace TibbiNav.Api.Controllers;

public record AssignTicketRequest(Guid? AssignedToUserId);
public record ChangeTicketStatusRequest(TicketStatus Status);
public record AddTicketCommentRequest(string Text, bool IsInternal);

/// <summary>
/// Раздел 54 ТЗ: обработка тикетов HR-специалистами — список (своей
/// clinic/organization scope, как и остальные HR-ресурсы), назначение, смена
/// статуса, комментарии (в т.ч. внутренние HR-заметки, недоступные
/// сотруднику — см. MeController). Создание тикета — только через
/// MeController (раздел 53: только сотрудник о самом себе).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/tickets")]
public class TicketsController(TibbiNavDbContext db, TicketService ticketService, IScopeContextAccessor scopeAccessor) : ControllerBase
{
    [HttpGet]
    [RequirePermission("Ticket", PermissionAction.View)]
    public async Task<IActionResult> List(
        [FromQuery] TicketStatus? status, [FromQuery] TicketCategory? category, [FromQuery] Guid? assignedToUserId, CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;
        var scopedEmployeeIds = db.Employees.AsNoTracking().ApplyEmployeeScope(scope, db).Select(e => e.Id);

        var query = db.Tickets.AsNoTracking().Where(t => scopedEmployeeIds.Contains(t.EmployeeId));
        if (status is not null) query = query.Where(t => t.Status == status);
        if (category is not null) query = query.Where(t => t.Category == category);
        if (assignedToUserId is not null) query = query.Where(t => t.AssignedToUserId == assignedToUserId);

        var tickets = await query.OrderByDescending(t => t.CreatedAtUtc).Take(200)
            .Select(t => new { t.Id, t.EmployeeId, t.Category, t.Subject, t.Status, t.AssignedToUserId, t.CreatedAtUtc })
            .ToListAsync(ct);

        return Ok(tickets);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("Ticket", PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var ticket = await GetInScopeAsync(id, ct);
        return ticket is null ? NotFound() : Ok(ToDetailDto(ticket));
    }

    [HttpPost("{id:guid}/assign")]
    [RequirePermission("Ticket", PermissionAction.Edit)]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignTicketRequest req, CancellationToken ct)
    {
        if (await GetInScopeAsync(id, ct) is null) return NotFound();

        var scope = scopeAccessor.Current!;
        try
        {
            var ticket = await ticketService.AssignAsync(id, req.AssignedToUserId ?? scope.UserId, ct);
            return Ok(new { ticket.Id, ticket.Status, ticket.AssignedToUserId });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/status")]
    [RequirePermission("Ticket", PermissionAction.Edit)]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeTicketStatusRequest req, CancellationToken ct)
    {
        if (await GetInScopeAsync(id, ct) is null) return NotFound();

        try
        {
            var ticket = await ticketService.ChangeStatusAsync(id, req.Status, ct);
            return Ok(new { ticket.Id, ticket.Status, ticket.ResolvedAtUtc, ticket.ClosedAtUtc });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/comments")]
    [RequirePermission("Ticket", PermissionAction.Edit)]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] AddTicketCommentRequest req, CancellationToken ct)
    {
        if (await GetInScopeAsync(id, ct) is null) return NotFound();

        var scope = scopeAccessor.Current!;
        try
        {
            var comment = await ticketService.AddCommentAsync(id, scope.UserId, req.Text, req.IsInternal, ct);
            return Ok(new { comment.Id, comment.Text, comment.IsInternal, comment.CreatedAtUtc });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private async Task<Ticket?> GetInScopeAsync(Guid id, CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;
        var scopedEmployeeIds = db.Employees.AsNoTracking().ApplyEmployeeScope(scope, db).Select(e => e.Id);
        return await db.Tickets.AsNoTracking().Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == id && scopedEmployeeIds.Contains(t.EmployeeId), ct);
    }

    private static object ToDetailDto(Ticket t) => new
    {
        t.Id,
        t.EmployeeId,
        t.Category,
        t.Subject,
        t.Description,
        t.Status,
        t.AssignedToUserId,
        t.CreatedAtUtc,
        t.ResolvedAtUtc,
        t.ClosedAtUtc,
        Comments = t.Comments.OrderBy(c => c.CreatedAtUtc)
            .Select(c => new { c.Id, c.AuthorUserId, c.Text, c.IsInternal, c.CreatedAtUtc }),
    };
}
