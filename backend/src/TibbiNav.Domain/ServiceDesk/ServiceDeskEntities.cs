using TibbiNav.Domain.Common;

namespace TibbiNav.Domain.ServiceDesk;

/// <summary>
/// Раздел 54 ТЗ: HR Service Desk — тикет от сотрудника. Создаётся только
/// сотрудником о самом себе (раздел 53: Employee Self Service,
/// см. MeController.CreateTicket — EmployeeId всегда = вызывающий пользователь,
/// не параметр запроса), обрабатывается HR (TicketsController).
/// </summary>
public class Ticket : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid? ClinicId { get; set; }
    public Guid EmployeeId { get; set; }

    public TicketCategory Category { get; set; }
    public string Subject { get; set; } = default!;
    public string Description { get; set; } = default!;

    public TicketStatus Status { get; set; } = TicketStatus.New;
    public Guid? AssignedToUserId { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }

    public ICollection<TicketComment> Comments { get; set; } = new List<TicketComment>();
}

/// <summary>Раздел 54: категории тикетов — кадровые документы, справки,
/// отпуск, изменение данных, обучение, зарплатный вопрос, доступ, другое.</summary>
public enum TicketCategory
{
    HrDocuments,
    Certificates,
    Leave,
    DataChange,
    Training,
    Payroll,
    Access,
    Other,
}

/// <summary>Раздел 54: New → Assigned → InProgress → Waiting → Resolved →
/// Closed. Допустимые переходы — см. TicketService.AllowedTransitions
/// (не любой статус из любого — данные/код, а не свободный enum-сеттер).</summary>
public enum TicketStatus
{
    New,
    Assigned,
    InProgress,
    Waiting,
    Resolved,
    Closed,
}

public class TicketComment : BaseEntity
{
    public Guid TicketId { get; set; }
    public Ticket Ticket { get; set; } = default!;

    public Guid AuthorUserId { get; set; }
    public string Text { get; set; } = default!;

    /// <summary>Внутренняя заметка HR, не видна сотруднику (см. MeController —
    /// самостоятельный комментарий сотрудника всегда IsInternal=false и
    /// список для сотрудника исключает внутренние заметки других).</summary>
    public bool IsInternal { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
