using TibbiNav.Domain.Common;

namespace TibbiNav.Domain.Core;

/// <summary>
/// Audit Log (раздел 67). Пишется через SaveChangesInterceptor в Infrastructure —
/// не через обычный DbSet.Add из бизнес-кода, чтобы гарантировать полноту.
/// Обычный администратор не имеет UPDATE/DELETE прав на эту таблицу (grant на уровне БД).
/// </summary>
public class AuditLogEntry : BaseEntity
{
    public Guid? UserId { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string? Ip { get; set; }
    public string Action { get; set; } = default!; // Create/Update/Delete/Approve/Export/Login...
    public string Entity { get; set; } = default!;
    public Guid? EntityId { get; set; }
    public string? OldValueJson { get; set; }
    public string? NewValueJson { get; set; }
}

/// <summary>Document Management (раздел 30). Сам файл — в S3-compatible storage
/// (раздел 80), тут только metadata.</summary>
public class EmployeeDocument : AuditableEntity
{
    public Guid EmployeeId { get; set; }
    public string DocumentType { get; set; } = default!; // "Contract", "Order", "NDA", ...
    public string FileKey { get; set; } = default!; // ключ в object storage
    public int Version { get; set; } = 1;
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
}

public enum DocumentStatus { Draft, Review, Approved, Signed, Archived, Cancelled }

/// <summary>Leave request (раздел 37-38). LeaveBalance считается в Application-слое
/// на основе LeaveType.AccrualRule + LeaveRequest history, не хранится как счётчик.</summary>
public class LeaveRequest : AuditableEntity
{
    public Guid EmployeeId { get; set; }
    public string LeaveType { get; set; } = default!; // "Annual", "Sick", "Unpaid", ...
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int Days { get; set; }
    public LeaveStatus Status { get; set; } = LeaveStatus.Requested;
    public string? Comment { get; set; }
}

public enum LeaveStatus { Requested, Approved, Rejected, Cancelled }
