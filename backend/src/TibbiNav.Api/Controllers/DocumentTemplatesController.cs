using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TibbiNav.Api.Authorization;
using TibbiNav.Domain.Core;
using TibbiNav.Domain.Documents;
using TibbiNav.Domain.Identity;
using TibbiNav.Infrastructure;

namespace TibbiNav.Api.Controllers;

public record CreateDocumentTemplateBlockRequest(DocumentBlockKind Kind, string Text, bool Bold, int OrderIndex);
public record CreateDocumentTemplateRequest(string Name, EmployeeDocumentType DocumentType, List<CreateDocumentTemplateBlockRequest> Blocks);

/// <summary>Раздел 29: управление шаблонами документов (трудовой договор,
/// приказ о приёме, NDA, согласие на обработку ПДн). На DocumentType — один
/// активный шаблон (см. DocumentGeneratorService); создание нового с тем же
/// типом не деактивирует старый автоматически — это осознанное решение HR.</summary>
[ApiController]
[Authorize]
[Route("api/v1/document-templates")]
public class DocumentTemplatesController(TibbiNavDbContext db) : ControllerBase
{
    [HttpGet]
    [RequirePermission("DocumentTemplate", PermissionAction.View)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var templates = await db.DocumentTemplates
            .AsNoTracking()
            .Include(t => t.Blocks)
            .OrderBy(t => t.DocumentType).ThenBy(t => t.Name)
            .ToListAsync(ct);

        return Ok(templates.Select(ToDto));
    }

    [HttpPost]
    [RequirePermission("DocumentTemplate", PermissionAction.Create)]
    public async Task<IActionResult> Create([FromBody] CreateDocumentTemplateRequest req, CancellationToken ct)
    {
        if (req.Blocks is null || req.Blocks.Count == 0)
            return BadRequest(new { error = "В шаблоне должен быть хотя бы один блок." });

        var template = new DocumentTemplate { Name = req.Name, DocumentType = req.DocumentType };

        foreach (var b in req.Blocks)
        {
            template.Blocks.Add(new DocumentTemplateBlock
            {
                Template = template,
                Kind = b.Kind,
                Text = b.Text,
                Bold = b.Bold,
                OrderIndex = b.OrderIndex,
            });
        }

        db.DocumentTemplates.Add(template);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(List), new { }, ToDto(template));
    }

    private static object ToDto(DocumentTemplate t) => new
    {
        t.Id,
        t.Name,
        t.DocumentType,
        t.IsActive,
        Blocks = t.Blocks.OrderBy(b => b.OrderIndex).Select(b => new { b.Id, b.Kind, b.Text, b.Bold, b.OrderIndex }),
    };
}
