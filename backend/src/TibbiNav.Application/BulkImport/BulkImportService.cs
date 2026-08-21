using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TibbiNav.Domain.BulkImport;
using TibbiNav.Infrastructure;

namespace TibbiNav.Application.BulkImport;

/// <summary>
/// Раздел 63 ТЗ: дирижирует пайплайном Upload → Mapping → Preview → Validation
/// → Import → Result, одинаково для любого ImportKind — конкретную логику
/// валидации/импорта берёт из соответствующего IBulkImportDefinition.
/// </summary>
public sealed class BulkImportService(TibbiNavDbContext db, IEnumerable<IBulkImportDefinition> definitions)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public IBulkImportDefinition GetDefinition(ImportKind kind) =>
        definitions.FirstOrDefault(d => d.Kind == kind)
            ?? throw new InvalidOperationException($"Нет обработчика импорта для {kind}.");

    /// <summary>Upload: разбирает XLSX и сразу пытается автосопоставить столбцы
    /// (раздел 63: Mapping — стартовая точка, не обязательный ручной шаг, если
    /// файл сделан по нашему шаблону).</summary>
    public async Task<ImportBatch> UploadAsync(ImportKind kind, Guid organizationId, Guid? clinicId, string fileName, Stream fileStream, CancellationToken ct)
    {
        var def = GetDefinition(kind);
        var parsed = XlsxParser.Parse(fileStream, def.TemplateSheetName);
        var suggestedMapping = def.SuggestMapping(parsed.Headers);

        var batch = new ImportBatch
        {
            OrganizationId = organizationId,
            ClinicId = clinicId,
            Kind = kind,
            FileName = fileName,
            Status = ImportBatchStatus.Uploaded,
            SourceHeadersJson = JsonSerializer.Serialize(parsed.Headers, JsonOpts),
            RawRowsJson = JsonSerializer.Serialize(parsed.Rows, JsonOpts),
            ColumnMappingJson = JsonSerializer.Serialize(suggestedMapping, JsonOpts),
            TotalRows = parsed.Rows.Count,
        };

        if (def.Fields.Where(f => f.Required).All(f => suggestedMapping.TryGetValue(f.Key, out var v) && v is not null))
            batch.Status = ImportBatchStatus.Mapped; // все обязательные поля нашлись сами — можно сразу превьюить

        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync(ct);
        return batch;
    }

    /// <summary>Mapping: явное переопределение автосопоставления столбцов.</summary>
    public async Task<ImportBatch> SetMappingAsync(Guid batchId, IDictionary<string, string?> mapping, CancellationToken ct)
    {
        var batch = await GetBatchOrThrowAsync(batchId, ct);
        batch.ColumnMappingJson = JsonSerializer.Serialize(mapping, JsonOpts);
        batch.Status = ImportBatchStatus.Mapped;
        await db.SaveChangesAsync(ct);
        return batch;
    }

    /// <summary>Preview: валидирует и мапит первые `take` строк без сохранения
    /// в БД и без записи в сам батч — быстрый визуальный контроль.</summary>
    public async Task<IReadOnlyList<ImportRowResult>> PreviewAsync(Guid batchId, int take, CancellationToken ct)
    {
        var batch = await GetBatchOrThrowAsync(batchId, ct);
        var def = GetDefinition(batch.Kind);
        var mapping = GetMapping(batch);
        var rows = GetRawRows(batch);

        var results = new List<ImportRowResult>();
        for (var i = 0; i < Math.Min(take, rows.Count); i++)
            results.Add(await def.ValidateRowAsync(i + 1, rows[i], mapping, db, batch.OrganizationId, ct));
        return results;
    }

    /// <summary>Validation: полная проверка всех строк файла, без записи в БД.</summary>
    public async Task<(ImportBatch Batch, IReadOnlyList<ImportRowResult> Rows)> ValidateAsync(Guid batchId, CancellationToken ct)
    {
        var batch = await GetBatchOrThrowAsync(batchId, ct);
        var def = GetDefinition(batch.Kind);
        var mapping = GetMapping(batch);
        var rows = GetRawRows(batch);

        var results = new List<ImportRowResult>();
        for (var i = 0; i < rows.Count; i++)
            results.Add(await def.ValidateRowAsync(i + 1, rows[i], mapping, db, batch.OrganizationId, ct));

        batch.ValidationResultJson = JsonSerializer.Serialize(
            results.Select(r => new { r.RowNumber, r.IsValid, r.Errors, r.Warnings }), JsonOpts);
        batch.ValidRows = results.Count(r => r.IsValid);
        batch.ErrorRows = results.Count(r => !r.IsValid);
        batch.Status = batch.ErrorRows == 0 ? ImportBatchStatus.Validated : ImportBatchStatus.ValidationFailed;
        batch.ValidatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return (batch, results);
    }

    /// <summary>Import: батч должен быть в статусе Validated. Строки
    /// перепроверяются последовательно внутри одной транзакции — благодаря
    /// этому, например, ФИО руководителя резолвится и на уже импортированную
    /// в этом же прогоне строку (см. EmployeeImportDefinition), а не только на
    /// то, что было в БД на момент /validate. Одна ошибка — откат всего
    /// батча целиком (all-or-nothing, раздел 63: bulk import не должен
    /// оставлять систему в наполовину согласованном состоянии).</summary>
    public async Task<(ImportBatch Batch, IReadOnlyList<ImportRowOutcome> Outcomes)> ImportAsync(Guid batchId, CancellationToken ct)
    {
        var batch = await GetBatchOrThrowAsync(batchId, ct);
        if (batch.Status != ImportBatchStatus.Validated)
            throw new InvalidOperationException($"Батч в статусе {batch.Status} — импорт возможен только из Validated. Сначала вызовите /validate.");

        var def = GetDefinition(batch.Kind);
        var mapping = GetMapping(batch);
        var rows = GetRawRows(batch);
        var importedCache = new Dictionary<string, Guid>();
        var outcomes = new List<ImportRowOutcome>();
        var hadError = false;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        for (var i = 0; i < rows.Count; i++)
        {
            var validated = await def.ValidateRowAsync(i + 1, rows[i], mapping, db, batch.OrganizationId, ct);
            if (!validated.IsValid)
            {
                hadError = true;
                outcomes.Add(new ImportRowOutcome(i + 1, false, false, true,
                    "Строка не прошла повторную валидацию на момент импорта: " + string.Join("; ", validated.Errors), null));
                continue;
            }
            outcomes.Add(await def.ImportRowAsync(validated, db, batch.OrganizationId, importedCache, ct));
        }

        if (hadError)
        {
            await tx.RollbackAsync(ct);
            batch.Status = ImportBatchStatus.ImportFailed;
            batch.ImportResultJson = JsonSerializer.Serialize(outcomes, JsonOpts);
            await db.SaveChangesAsync(ct);
            return (batch, outcomes);
        }

        await tx.CommitAsync(ct);

        batch.CreatedCount = outcomes.Count(o => o.Created);
        batch.UpdatedCount = outcomes.Count(o => o.Updated);
        batch.SkippedCount = outcomes.Count(o => o.Skipped);
        batch.ImportResultJson = JsonSerializer.Serialize(outcomes, JsonOpts);
        batch.Status = ImportBatchStatus.Imported;
        batch.ImportedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return (batch, outcomes);
    }

    public async Task<ImportBatch> GetBatchOrThrowAsync(Guid batchId, CancellationToken ct) =>
        await db.ImportBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct)
            ?? throw new KeyNotFoundException("Импорт-батч не найден.");

    public static IReadOnlyDictionary<string, string?> GetMapping(ImportBatch batch) =>
        string.IsNullOrWhiteSpace(batch.ColumnMappingJson)
            ? new Dictionary<string, string?>()
            : JsonSerializer.Deserialize<Dictionary<string, string?>>(batch.ColumnMappingJson, JsonOpts) ?? new();

    public static IReadOnlyList<string> GetSourceHeaders(ImportBatch batch) =>
        JsonSerializer.Deserialize<List<string>>(batch.SourceHeadersJson, JsonOpts) ?? [];

    public static List<IReadOnlyDictionary<string, string>> GetRawRows(ImportBatch batch) =>
        (JsonSerializer.Deserialize<List<Dictionary<string, string>>>(batch.RawRowsJson, JsonOpts) ?? [])
            .Select(d => (IReadOnlyDictionary<string, string>)d)
            .ToList();
}
