using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TibbiNav.Api.Authorization;
using TibbiNav.Application.Authorization;
using TibbiNav.Application.Documents;
using TibbiNav.Domain.Core;
using TibbiNav.Domain.Identity;
using TibbiNav.Infrastructure;

namespace TibbiNav.Api.Controllers;

public record GenerateEmployeeDocumentRequest(Guid EmployeeId, EmployeeDocumentType DocumentType);

/// <summary>
/// Раздел 29-30: документы сотрудников, сгенерированные из шаблона. Обычно
/// создаются автоматически из HireCandidateService — эндпоинты здесь для
/// просмотра, скачивания (DOCX/PDF), ручной генерации и статусных переходов
/// Draft → Review → Approved → Signed → Archived (Cancelled — из любого
/// нетерминального статуса). Approve/Sign вынесены в отдельные шаги с
/// Permission "Approve" — это более весомое действие, чем обычное
/// редактирование черновика.
/// Scope (раздел 65) — как и у чеклиста адаптации: документ привязан к
/// конкретному сотруднику, фильтруется через тот же ApplyEmployeeScope.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/employee-documents")]
public class EmployeeDocumentsController(TibbiNavDbContext db, IScopeContextAccessor scopeAccessor, DocumentGeneratorService documentGeneratorService) : ControllerBase
{
    [HttpGet]
    [RequirePermission("EmployeeDocument", PermissionAction.View)]
    public async Task<IActionResult> List([FromQuery] Guid? employeeId, [FromQuery] DocumentStatus? status, CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;
        var scopedEmployeeIds = db.Employees.AsNoTracking().ApplyEmployeeScope(scope, db).Select(e => e.Id);

        var query = db.EmployeeDocuments.AsNoTracking().Where(d => scopedEmployeeIds.Contains(d.EmployeeId));
        if (employeeId is not null) query = query.Where(d => d.EmployeeId == employeeId);
        if (status is not null) query = query.Where(d => d.Status == status);

        var documents = await query.OrderByDescending(d => d.GeneratedAtUtc).Take(200).ToListAsync(ct);
        return Ok(documents.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("EmployeeDocument", PermissionAction.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var document = await GetInScopeAsync(id, ct);
        return document is null ? NotFound() : Ok(ToDto(document));
    }

    /// <summary>Раздел 29: скачивание сгенерированного файла — `?format=docx`
    /// (по умолчанию) или `?format=pdf`.</summary>
    [HttpGet("{id:guid}/file")]
    [RequirePermission("EmployeeDocument", PermissionAction.View)]
    public async Task<IActionResult> DownloadFile(Guid id, [FromQuery] string format = "docx", CancellationToken ct = default)
    {
        if (await GetInScopeAsync(id, ct) is null) return NotFound();

        try
        {
            var (content, fileName, contentType) = await documentGeneratorService.GetFileAsync(id, format, ct);
            return File(content, contentType, fileName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Ручная генерация — напр. перевыпуск NDA, или документ, не
    /// сгенерированный при найме из-за отсутствовавшего тогда шаблона.</summary>
    [HttpPost("generate")]
    [RequirePermission("EmployeeDocument", PermissionAction.Create)]
    public async Task<IActionResult> Generate([FromBody] GenerateEmployeeDocumentRequest req, CancellationToken ct)
    {
        try
        {
            var document = await documentGeneratorService.GenerateAndSaveAsync(req.EmployeeId, req.DocumentType, ct);
            return document is null
                ? NotFound(new { error = "Нет активного шаблона для этого типа документа." })
                : Ok(ToDto(document));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/submit-for-review")]
    [RequirePermission("EmployeeDocument", PermissionAction.Edit)]
    public Task<IActionResult> SubmitForReview(Guid id, CancellationToken ct) =>
        TransitionAsync(id, ct, () => documentGeneratorService.SubmitForReviewAsync(id, ct));

    [HttpPost("{id:guid}/approve")]
    [RequirePermission("EmployeeDocument", PermissionAction.Approve)]
    public Task<IActionResult> Approve(Guid id, CancellationToken ct) =>
        TransitionAsync(id, ct, () => documentGeneratorService.ApproveAsync(id, scopeAccessor.Current!.UserId, ct));

    [HttpPost("{id:guid}/sign")]
    [RequirePermission("EmployeeDocument", PermissionAction.Approve)]
    public Task<IActionResult> Sign(Guid id, CancellationToken ct) =>
        TransitionAsync(id, ct, () => documentGeneratorService.SignAsync(id, scopeAccessor.Current!.UserId, ct));

    [HttpPost("{id:guid}/archive")]
    [RequirePermission("EmployeeDocument", PermissionAction.Edit)]
    public Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        TransitionAsync(id, ct, () => documentGeneratorService.ArchiveAsync(id, ct));

    [HttpPost("{id:guid}/cancel")]
    [RequirePermission("EmployeeDocument", PermissionAction.Edit)]
    public Task<IActionResult> Cancel(Guid id, CancellationToken ct) =>
        TransitionAsync(id, ct, () => documentGeneratorService.CancelAsync(id, ct));

    private async Task<IActionResult> TransitionAsync(Guid id, CancellationToken ct, Func<Task<EmployeeDocument>> transition)
    {
        if (await GetInScopeAsync(id, ct) is null) return NotFound();

        try
        {
            var document = await transition();
            return Ok(ToDto(document));
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

    private async Task<EmployeeDocument?> GetInScopeAsync(Guid id, CancellationToken ct)
    {
        var scope = scopeAccessor.Current!;
        var scopedEmployeeIds = db.Employees.AsNoTracking().ApplyEmployeeScope(scope, db).Select(e => e.Id);
        return await db.EmployeeDocuments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && scopedEmployeeIds.Contains(d.EmployeeId), ct);
    }

    private static object ToDto(EmployeeDocument d) => new
    {
        d.Id,
        d.EmployeeId,
        d.EmploymentRecordId,
        d.TemplateId,
        d.DocumentType,
        d.Title,
        d.Version,
        d.Status,
        d.GeneratedAtUtc,
        d.ApprovedByUserId,
        d.ApprovedAtUtc,
        d.SignedByUserId,
        d.SignedAtUtc,
        HasDocx = d.DocxFileKey is not null,
        HasPdf = d.PdfFileKey is not null,
    };
}
