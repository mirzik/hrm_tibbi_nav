using TibbiNav.Domain.BulkImport;
using TibbiNav.Infrastructure;

namespace TibbiNav.Application.BulkImport;

/// <summary>Описание одного целевого поля импорта — для UI Mapping-шага и для
/// автосопоставления заголовков файла по Label.</summary>
public sealed record ImportFieldDef(string Key, string Label, bool Required, string? Hint = null);

/// <summary>Результат валидации одной строки (Validation step, раздел 63).</summary>
public sealed class ImportRowResult
{
    public required int RowNumber { get; init; } // 1-based, по порядку в файле (без заголовка)
    public required bool IsValid { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>Значения после парсинга/приведения типов и разрешения справочников
    /// (напр. ClinicId вместо кода клиники) — то, что реально пойдёт на запись.</summary>
    public IReadOnlyDictionary<string, object?> MappedValues { get; init; } = new Dictionary<string, object?>();
}

/// <summary>Результат импорта одной строки (Import step).</summary>
public sealed record ImportRowOutcome(int RowNumber, bool Created, bool Updated, bool Skipped, string? Note, Guid? EntityId);

/// <summary>
/// Раздел 63: стратегия под конкретный ImportKind (штатное расписание /
/// сотрудники) — знает целевые поля, как валидировать строку против БД и как
/// её импортировать. BulkImportService дирижирует пайплайном одинаково для
/// обоих кинов, вызывая методы этого интерфейса.
/// </summary>
public interface IBulkImportDefinition
{
    ImportKind Kind { get; }
    IReadOnlyList<ImportFieldDef> Fields { get; }

    /// <summary>Имя листа в сгенерированном шаблоне (BulkImportTemplateGenerator) —
    /// используется и при Upload, чтобы из многолистового файла выбрать
    /// правильный лист по Kind (см. XlsxParser.Parse).</summary>
    string TemplateSheetName { get; }

    /// <summary>Автосопоставление целевое поле → исходный столбец по совпадению
    /// Label с заголовком файла (без учёта регистра) — стартовая точка для
    /// Mapping-шага, пользователь может переопределить.</summary>
    IDictionary<string, string?> SuggestMapping(IReadOnlyList<string> sourceHeaders)
    {
        var result = new Dictionary<string, string?>();
        foreach (var field in Fields)
        {
            var match = sourceHeaders.FirstOrDefault(h => string.Equals(NormalizeHeader(h), NormalizeHeader(field.Label), StringComparison.OrdinalIgnoreCase));
            result[field.Key] = match;
        }
        return result;
    }

    /// <summary>Убирает суффикс "*", которым BulkImportTemplateGenerator помечает
    /// обязательные столбцы в сгенерированном шаблоне, — иначе "Код клиники *"
    /// не сматчится с полем Label="Код клиники" при автосопоставлении.</summary>
    private static string NormalizeHeader(string header) => header.Trim().TrimEnd('*').Trim();

    Task<ImportRowResult> ValidateRowAsync(
        int rowNumber,
        IReadOnlyDictionary<string, string> rawRow,
        IReadOnlyDictionary<string, string?> mapping,
        TibbiNavDbContext db,
        Guid organizationId,
        CancellationToken ct);

    /// <summary>Импортирует одну валидную строку. Строки передаются по порядку
    /// файла, поэтому определение может ссылаться на уже созданные в этом же
    /// батче сущности (напр. Position, созданная предыдущей строкой этого же
    /// файла) через `importedInBatch`.</summary>
    Task<ImportRowOutcome> ImportRowAsync(
        ImportRowResult validatedRow,
        TibbiNavDbContext db,
        Guid organizationId,
        IDictionary<string, Guid> importedInBatch,
        CancellationToken ct);
}
