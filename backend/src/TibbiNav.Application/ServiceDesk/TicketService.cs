using Microsoft.EntityFrameworkCore;
using TibbiNav.Domain.ServiceDesk;
using TibbiNav.Infrastructure;

namespace TibbiNav.Application.ServiceDesk;

/// <summary>
/// Раздел 54 ТЗ: HR Service Desk. Тикет создаёт только сотрудник о самом себе
/// (раздел 53 — см. MeController.CreateTicket), обрабатывает HR
/// (TicketsController): назначение, смена статуса по фиксированному графу
/// переходов, комментарии (открытые сотруднику или внутренние HR-заметки).
/// </summary>
public sealed class TicketService(TibbiNavDbContext db)
{
    /// <summary>Раздел 54: New → Assigned → InProgress → Waiting → Resolved →
    /// Closed, плюс реалистичные короткие пути (New/Waiting напрямую в
    /// InProgress/Resolved, Resolved обратно в InProgress при повторном
    /// открытии) — не любой статус из любого.</summary>
    private static readonly IReadOnlyDictionary<TicketStatus, TicketStatus[]> AllowedTransitions = new Dictionary<TicketStatus, TicketStatus[]>
    {
        [TicketStatus.New] = [TicketStatus.Assigned, TicketStatus.InProgress],
        [TicketStatus.Assigned] = [TicketStatus.InProgress],
        [TicketStatus.InProgress] = [TicketStatus.Waiting, TicketStatus.Resolved],
        [TicketStatus.Waiting] = [TicketStatus.InProgress, TicketStatus.Resolved],
        [TicketStatus.Resolved] = [TicketStatus.Closed, TicketStatus.InProgress],
        [TicketStatus.Closed] = [],
    };

    public async Task<Ticket> CreateAsync(Guid employeeId, TicketCategory category, string subject, string description, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new InvalidOperationException("Тема тикета обязательна.");

        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct)
            ?? throw new InvalidOperationException("Сотрудник не найден.");

        var ticket = new Ticket
        {
            OrganizationId = employee.OrganizationId,
            ClinicId = employee.ClinicId,
            EmployeeId = employeeId,
            Category = category,
            Subject = subject,
            Description = description,
            Status = TicketStatus.New,
        };
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(ct);
        return ticket;
    }

    /// <summary>Назначение тикета исполнителю — допустимо из New/Assigned
    /// (переназначение), переводит в Assigned.</summary>
    public async Task<Ticket> AssignAsync(Guid ticketId, Guid assignedToUserId, CancellationToken ct)
    {
        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct)
            ?? throw new KeyNotFoundException("Тикет не найден.");

        if (ticket.Status is not (TicketStatus.New or TicketStatus.Assigned))
            throw new InvalidOperationException($"Нельзя назначить тикет в статусе {ticket.Status}.");

        ticket.AssignedToUserId = assignedToUserId;
        ticket.Status = TicketStatus.Assigned;
        await db.SaveChangesAsync(ct);
        return ticket;
    }

    public async Task<Ticket> ChangeStatusAsync(Guid ticketId, TicketStatus newStatus, CancellationToken ct)
    {
        var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct)
            ?? throw new KeyNotFoundException("Тикет не найден.");

        if (!AllowedTransitions.TryGetValue(ticket.Status, out var allowed) || !allowed.Contains(newStatus))
        {
            var allowedText = allowed is { Length: > 0 } ? string.Join(", ", allowed) : "нет (терминальный статус)";
            throw new InvalidOperationException($"Нельзя перейти в статус {newStatus} из {ticket.Status}. Допустимо: {allowedText}.");
        }

        ticket.Status = newStatus;
        if (newStatus == TicketStatus.Resolved) ticket.ResolvedAtUtc = DateTime.UtcNow;
        if (newStatus == TicketStatus.Closed) ticket.ClosedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return ticket;
    }

    public async Task<TicketComment> AddCommentAsync(Guid ticketId, Guid authorUserId, string text, bool isInternal, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Текст комментария обязателен.");

        var exists = await db.Tickets.AsNoTracking().AnyAsync(t => t.Id == ticketId, ct);
        if (!exists) throw new KeyNotFoundException("Тикет не найден.");

        // db.TicketComments.Add(...) напрямую (не через ticket.Comments.Add) —
        // см. историю бага в TimesheetService: явный Add на DbSet, а не
        // fixup через навигацию уже отслеживаемого родителя.
        var comment = new TicketComment
        {
            TicketId = ticketId,
            AuthorUserId = authorUserId,
            Text = text,
            IsInternal = isInternal,
        };
        db.TicketComments.Add(comment);
        await db.SaveChangesAsync(ct);
        return comment;
    }
}
