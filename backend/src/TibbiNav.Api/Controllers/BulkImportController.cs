using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TibbiNav.Api.Authorization;
using TibbiNav.Application.BulkImport;
using TibbiNav.Domain.BulkImport;
using TibbiNav.Domain.Identity;

namespace TibbiNav.Api.Controllers;

/// <summary>
/// Раздел 63 ТЗ: Bulk Import штатного расписания и базы сотрудников.
/// Пайплайн — по одному endpoint-у на шаг:
///   POST /{kind}/upload      — Upload (парсинг файла + автосопоставление столбцов)
///   PUT  /{id}/mapping       — Mapping (ручное переопределение сопоставления)
///   GET  /{id}/preview       — Preview (валидация + мапинг первых N строк, без записи)
///   POST /{id}/validate      — Validation (полная проверка всех строк, без записи)
///   POST /{id}/import        — Import (запись в БД одной транзакцией — только из Validated)
///   GET  /{id}                — Result (текущее состояние/итоги батча)
///   GET  /template             — образец XLSX для заполнения
///
/// Permission "Approve" зарезервирован под сам /import — это необратимый шаг,
/// остальные (Upload/Mapping/Preview/Validate) — подготовительные, требуют
/// только Create; чтение результата — View.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/bulk-import")]
public class BulkImportController(BulkImportService importService, BulkImportTemplateGenerator templateGenerator) : ControllerBase
{
    [HttpPost("{kind}/upload")]
    [RequirePermission("BulkImport", PermissionAction.Create)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> Upload(ImportKind kind, [FromQuery] Guid organizationId, [FromQuery] Guid? clinicId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Файл не передан или пуст." });

        try
        {
            await using var stream = file.OpenReadStream();
            var batch = await importService.UploadAsync(kind, organizationId, clinicId, file.FileName, stream, ct);
            return Ok(ToBatchDto(batch));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{batchId:guid}/mapping")]
    [RequirePermission("BulkImport", PermissionAction.Create)]
    public async Task<IActionResult> SetMapping(Guid batchId, [FromBody] Dictionary<string, string?> mapping, CancellationToken ct)
    {
        try
        {
            var batch = await importService.SetMappingAsync(batchId, mapping, ct);
            return Ok(ToBatchDto(batch));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("{batchId:guid}/preview")]
    [RequirePermission("BulkImport", PermissionAction.View)]
    public async Task<IActionResult> Preview(Guid batchId, [FromQuery] int take = 20, CancellationToken ct = default)
    {
        try
        {
            var rows = await importService.PreviewAsync(batchId, take, ct);
            return Ok(rows.Select(ToRowDto));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{batchId:guid}/validate")]
    [RequirePermission("BulkImport", PermissionAction.Create)]
    public async Task<IActionResult> Validate(Guid batchId, CancellationToken ct)
    {
        try
        {
            var (batch, rows) = await importService.ValidateAsync(batchId, ct);
            return Ok(new { Batch = ToBatchDto(batch), Rows = rows.Select(ToRowDto) });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Раздел 63: сам импорт — необратимо пишет данные в БД, требует Approve.</summary>
    [HttpPost("{batchId:guid}/import")]
    [RequirePermission("BulkImport", PermissionAction.Approve)]
    public async Task<IActionResult> Import(Guid batchId, CancellationToken ct)
    {
        try
        {
            var (batch, outcomes) = await importService.ImportAsync(batchId, ct);
            return Ok(new { Batch = ToBatchDto(batch), Outcomes = outcomes });
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

    [HttpGet("{batchId:guid}")]
    [RequirePermission("BulkImport", PermissionAction.View)]
    public async Task<IActionResult> GetBatch(Guid batchId, CancellationToken ct)
    {
        try
        {
            var batch = await importService.GetBatchOrThrowAsync(batchId, ct);
            return Ok(ToBatchDto(batch));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Раздел 63: образец XLSX-шаблона (штатное расписание + сотрудники +
    /// справочник допустимых значений) — колонки строятся из тех же
    /// IBulkImportDefinition.Fields, что используются при валидации.</summary>
    [HttpGet("template")]
    [RequirePermission("BulkImport", PermissionAction.View)]
    public IActionResult DownloadTemplate()
    {
        var bytes = templateGenerator.GenerateWorkbook();
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "tibbinav-bulk-import-template.xlsx");
    }

    private object ToBatchDto(ImportBatch batch) => new
    {
        batch.Id,
        batch.Kind,
        batch.FileName,
        batch.Status,
        batch.TotalRows,
        batch.ValidRows,
        batch.ErrorRows,
        batch.CreatedCount,
        batch.UpdatedCount,
        batch.SkippedCount,
        batch.ValidatedAtUtc,
        batch.ImportedAtUtc,
        Fields = importService.GetDefinition(batch.Kind).Fields,
        SourceHeaders = BulkImportService.GetSourceHeaders(batch),
        Mapping = BulkImportService.GetMapping(batch),
    };

    private static object ToRowDto(ImportRowResult r) => new
    {
        r.RowNumber,
        r.IsValid,
        r.Errors,
        r.Warnings,
        Values = r.MappedValues,
    };
}
