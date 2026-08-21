using TibbiNav.Domain.Common;

namespace TibbiNav.Domain.BulkImport;

/// <summary>
/// Раздел 63 ТЗ: Bulk Import существующих Excel-данных компании. Один
/// ImportBatch = один загруженный XLSX-файл, проходящий пайплайн
/// Upload → Mapping → Preview → Validation → Import → Result (см.
/// TibbiNav.Application.BulkImport.BulkImportService).
///
/// Разобранные строки и результаты хранятся как JSON прямо на батче — схема
/// исходного файла произвольна (зависит от того, как экспортировала 1С/Excel
/// компания), поэтому нет смысла заводить под неё жёсткую реляционную таблицу.
/// </summary>
public class ImportBatch : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid? ClinicId { get; set; }

    public ImportKind Kind { get; set; }
    public string FileName { get; set; } = default!;
    public ImportBatchStatus Status { get; set; } = ImportBatchStatus.Uploaded;

    /// <summary>Заголовки столбцов, как они пришли из файла (сохраняем порядок).</summary>
    public string SourceHeadersJson { get; set; } = "[]";

    /// <summary>Сырые строки файла: List&lt;Dictionary&lt;string,string&gt;&gt; (заголовок → значение ячейки).</summary>
    public string RawRowsJson { get; set; } = "[]";

    /// <summary>Маппинг "целевое поле → исходный столбец" (раздел 63: Mapping step).
    /// Заполняется автоматически при Upload по совпадению заголовков, может быть
    /// переопределён явным вызовом PUT .../mapping.</summary>
    public string? ColumnMappingJson { get; set; }

    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int ErrorRows { get; set; }

    /// <summary>Результат последней валидации: List&lt;ImportRowResult&gt; (Application-слой).</summary>
    public string? ValidationResultJson { get; set; }

    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }

    /// <summary>Результат последнего импорта: List&lt;ImportRowOutcome&gt; (Application-слой).</summary>
    public string? ImportResultJson { get; set; }

    public DateTime? ValidatedAtUtc { get; set; }
    public DateTime? ImportedAtUtc { get; set; }
}

/// <summary>Раздел 63: два критичных по срокам набора данных для переноса —
/// штатное расписание (Position) и текущая база сотрудников (Employee).</summary>
public enum ImportKind
{
    StaffingSchedule,
    Employees,
}

public enum ImportBatchStatus
{
    Uploaded,
    Mapped,
    Validated,
    ValidationFailed,
    Imported,
    ImportFailed,
}
