namespace TibbiNav.Domain.Common;

/// <summary>
/// Базовый класс для всех сущностей. Guid PK — удобно для распределённых
/// импортов/экспортов и не раскрывает объём данных (в отличие от auto-increment int).
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}

/// <summary>
/// Сущности, требующие полного audit trail (кто/когда создал и изменил).
/// Audit Log (раздел 67 ТЗ) пишется отдельно через AuditInterceptor,
/// это — быстрый snapshot прямо на записи для UI.
/// </summary>
public abstract class AuditableEntity : BaseEntity
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? CreatedByUserId { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public Guid? ModifiedByUserId { get; set; }

    /// <summary>Soft delete — используется только там, где оправдано (раздел 83).</summary>
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
}

/// <summary>
/// Multi-clinic scope (раздел 4 ТЗ). Любая сущность, привязанная к оргструктуре,
/// реализует этот интерфейс — никакого "if clinic == ..." в коде, только данные.
/// </summary>
public interface IOrganizationScoped
{
    Guid OrganizationId { get; set; }
    Guid? ClinicId { get; set; }
}

/// <summary>
/// Effective Dating (раздел 10 ТЗ): вместо перезаписи кадровых данных
/// создаётся новая версия записи с датой начала действия. Старая версия
/// закрывается EffectiveTo, история не удаляется.
/// </summary>
public abstract class EffectiveDatedEntity : AuditableEntity
{
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsCurrent { get; set; } = true;
}
