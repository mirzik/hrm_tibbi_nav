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

/// <summary>
/// Document Management (раздел 30) + результат работы Document Generator-а
/// (раздел 29, см. TibbiNav.Application.Documents.DocumentGeneratorService).
/// Сам файл — в файловом хранилище (IDocumentFileStorage; сейчас локальный
/// диск, в проде заменяется на S3-compatible без изменения домена, раздел 80),
/// тут только metadata + статус согласования.
/// </summary>
public class EmployeeDocument : AuditableEntity
{
    public Guid EmployeeId { get; set; }

    /// <summary>Снимок EmploymentRecord, из данных которого документ был
    /// сгенерирован (Effective Dating, раздел 10) — для аудита: по какой
    /// должности/окладу составлен документ, даже если они позже изменились.</summary>
    public Guid EmploymentRecordId { get; set; }

    public Guid TemplateId { get; set; }
    public EmployeeDocumentType DocumentType { get; set; }
    public string Title { get; set; } = default!;

    public string DocxFileKey { get; set; } = default!; // ключ .docx в хранилище
    public string? PdfFileKey { get; set; } // ключ .pdf в хранилище

    public int Version { get; set; } = 1;
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public Guid? SignedByUserId { get; set; }
    public DateTime? SignedAtUtc { get; set; }
}

/// <summary>Раздел 29: типы документов, для которых поддерживается генерация
/// из шаблона. Расширяется добавлением значения в enum + нового
/// DocumentTemplate — код генератора типо-агностичен.</summary>
public enum EmployeeDocumentType
{
    EmploymentContract,
    HireOrder,
    Nda,
    PersonalDataConsent,
}

/// <summary>Раздел 29-30: жизненный цикл документа. Cancelled достижим из
/// любого нетерминального статуса (см. DocumentGeneratorService).</summary>
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
