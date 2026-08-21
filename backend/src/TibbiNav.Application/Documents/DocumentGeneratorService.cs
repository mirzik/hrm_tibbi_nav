using Microsoft.EntityFrameworkCore;
using TibbiNav.Domain.Core;
using TibbiNav.Domain.Documents;
using TibbiNav.Domain.Employees;
using TibbiNav.Domain.Organization;
using TibbiNav.Infrastructure;

namespace TibbiNav.Application.Documents;

/// <summary>
/// Раздел 29 ТЗ: генерирует документы (трудовой договор, приказ о приёме,
/// NDA, согласие на обработку ПДн) из активного DocumentTemplate + данных
/// Employee/EmploymentRecord, экспортирует в DOCX и PDF. Стандартный пакет
/// запускается автоматически из HireCandidateService сразу после найма (той
/// же транзакцией — см. GenerateStandardHirePacketAsync, аналогично
/// OnboardingChecklistService), плюс ручной запуск одного документа через API
/// (GenerateAndSaveAsync).
/// </summary>
public sealed class DocumentGeneratorService(
    TibbiNavDbContext db,
    DocumentPlaceholderResolver placeholderResolver,
    DocxDocumentRenderer docxRenderer,
    PdfDocumentRenderer pdfRenderer,
    IDocumentFileStorage storage)
{
    private static readonly EmployeeDocumentType[] StandardHirePacket =
    [
        EmployeeDocumentType.EmploymentContract,
        EmployeeDocumentType.HireOrder,
        EmployeeDocumentType.Nda,
        EmployeeDocumentType.PersonalDataConsent,
    ];

    /// <summary>Раздел 29: весь стандартный пакет документов при найме, атомарно
    /// с ним (не вызывает SaveChanges — рассчитан на вызов внутри уже открытой
    /// транзакции HireCandidateService). Тип документа, для которого ещё нет
    /// активного шаблона, молча пропускается — найм не должен блокироваться
    /// из-за того, что HR не настроил какой-то из шаблонов.</summary>
    public async Task<List<EmployeeDocument>> GenerateStandardHirePacketAsync(
        Employee employee, EmploymentRecord employment, Position position, CancellationToken ct)
    {
        var placeholders = await placeholderResolver.ResolveAsync(employee, employment, position, ct);

        var result = new List<EmployeeDocument>();
        foreach (var type in StandardHirePacket)
        {
            var document = await GenerateCoreAsync(employee.Id, employment.Id, type, placeholders, ct);
            if (document is not null) result.Add(document);
        }
        return result;
    }

    /// <summary>Ручной запуск одного документа (напр. перевыпуск NDA, или
    /// документ, пропущенный при найме из-за отсутствовавшего тогда шаблона).
    /// Сам загружает текущие Employee/EmploymentRecord/Position и вызывает
    /// SaveChanges.</summary>
    public async Task<EmployeeDocument?> GenerateAndSaveAsync(Guid employeeId, EmployeeDocumentType type, CancellationToken ct)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct)
            ?? throw new KeyNotFoundException("Сотрудник не найден.");
        var employment = await db.EmploymentRecords.FirstOrDefaultAsync(r => r.EmployeeId == employeeId && r.IsCurrent, ct)
            ?? throw new InvalidOperationException("У сотрудника нет текущей записи трудоустройства (EmploymentRecord).");
        var position = await db.Positions.FirstAsync(p => p.Id == employment.PositionId, ct);

        var placeholders = await placeholderResolver.ResolveAsync(employee, employment, position, ct);
        var document = await GenerateCoreAsync(employee.Id, employment.Id, type, placeholders, ct);
        if (document is not null) await db.SaveChangesAsync(ct);
        return document;
    }

    private async Task<EmployeeDocument?> GenerateCoreAsync(
        Guid employeeId, Guid employmentRecordId, EmployeeDocumentType type,
        IReadOnlyDictionary<string, string> placeholders, CancellationToken ct)
    {
        var template = await db.DocumentTemplates
            .Include(t => t.Blocks)
            .Where(t => t.IsActive && t.DocumentType == type)
            .FirstOrDefaultAsync(ct);
        if (template is null) return null;

        var resolvedBlocks = template.Blocks
            .OrderBy(b => b.OrderIndex)
            .Select(b => new ResolvedDocumentBlock(b.Kind, DocumentPlaceholderResolver.Substitute(b.Text, placeholders), b.Bold))
            .ToList();

        var docxBytes = docxRenderer.Render(resolvedBlocks);
        var pdfBytes = pdfRenderer.Render(resolvedBlocks);

        var documentId = Guid.NewGuid();
        var docxKey = $"employees/{employeeId}/{documentId}.docx";
        var pdfKey = $"employees/{employeeId}/{documentId}.pdf";
        await storage.SaveAsync(docxKey, docxBytes, ct);
        await storage.SaveAsync(pdfKey, pdfBytes, ct);

        var document = new EmployeeDocument
        {
            Id = documentId,
            EmployeeId = employeeId,
            EmploymentRecordId = employmentRecordId,
            TemplateId = template.Id,
            DocumentType = type,
            Title = template.Name,
            DocxFileKey = docxKey,
            PdfFileKey = pdfKey,
            Status = DocumentStatus.Draft,
            GeneratedAtUtc = DateTime.UtcNow,
        };
        db.EmployeeDocuments.Add(document);
        return document;
    }

    // --- Раздел 29-30: статусные переходы Draft → Review → Approved → Signed → Archived,
    // Cancelled достижим из любого нетерминального статуса. ---

    public Task<EmployeeDocument> SubmitForReviewAsync(Guid id, CancellationToken ct) =>
        TransitionAsync(id, [DocumentStatus.Draft, DocumentStatus.Review], DocumentStatus.Review, ct);

    public Task<EmployeeDocument> ApproveAsync(Guid id, Guid userId, CancellationToken ct) =>
        TransitionAsync(id, [DocumentStatus.Review], DocumentStatus.Approved, ct,
            d => { d.ApprovedByUserId = userId; d.ApprovedAtUtc = DateTime.UtcNow; });

    public Task<EmployeeDocument> SignAsync(Guid id, Guid userId, CancellationToken ct) =>
        TransitionAsync(id, [DocumentStatus.Approved], DocumentStatus.Signed, ct,
            d => { d.SignedByUserId = userId; d.SignedAtUtc = DateTime.UtcNow; });

    public Task<EmployeeDocument> ArchiveAsync(Guid id, CancellationToken ct) =>
        TransitionAsync(id, [DocumentStatus.Signed], DocumentStatus.Archived, ct);

    public Task<EmployeeDocument> CancelAsync(Guid id, CancellationToken ct) =>
        TransitionAsync(id, [DocumentStatus.Draft, DocumentStatus.Review, DocumentStatus.Approved], DocumentStatus.Cancelled, ct);

    private async Task<EmployeeDocument> TransitionAsync(
        Guid id, DocumentStatus[] allowedFrom, DocumentStatus to, CancellationToken ct, Action<EmployeeDocument>? mutate = null)
    {
        var document = await db.EmployeeDocuments.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new KeyNotFoundException("Документ не найден.");

        if (!allowedFrom.Contains(document.Status))
            throw new InvalidOperationException(
                $"Нельзя перейти в статус {to} из {document.Status}. Допустимо из: {string.Join(", ", allowedFrom)}.");

        document.Status = to;
        mutate?.Invoke(document);

        await db.SaveChangesAsync(ct);
        return document;
    }

    /// <summary>Раздел 29: отдаёт содержимое сгенерированного файла для скачивания.</summary>
    public async Task<(byte[] Content, string FileName, string ContentType)> GetFileAsync(Guid documentId, string format, CancellationToken ct)
    {
        var document = await db.EmployeeDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw new KeyNotFoundException("Документ не найден.");

        var isPdf = string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase);
        var key = isPdf ? document.PdfFileKey : document.DocxFileKey;
        if (key is null)
            throw new InvalidOperationException($"Файл в формате {format} для этого документа не был сгенерирован.");

        var content = await storage.ReadAsync(key, ct);
        var extension = isPdf ? "pdf" : "docx";
        var contentType = isPdf
            ? "application/pdf"
            : "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

        return (content, $"{document.Title}.{extension}", contentType);
    }
}
