using TibbiNav.Domain.Common;
using TibbiNav.Domain.Core;

namespace TibbiNav.Domain.Documents;

/// <summary>
/// Раздел 29 ТЗ: шаблон документа, генерируемого из данных Employee/
/// EmploymentRecord. Body — упорядоченный список блоков (Blocks) с
/// плейсхолдерами вида {{FullName}} — единое дерево, из которого рендерятся
/// и DOCX, и PDF (см. TibbiNav.Application.Documents), поэтому форматы не
/// могут разойтись между собой. Активный шаблон на DocumentType — один
/// (см. DocumentGeneratorService: `Where(t => t.IsActive && t.DocumentType == type)`).
/// </summary>
public class DocumentTemplate : AuditableEntity
{
    public string Name { get; set; } = default!;
    public EmployeeDocumentType DocumentType { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<DocumentTemplateBlock> Blocks { get; set; } = new List<DocumentTemplateBlock>();
}

public class DocumentTemplateBlock : BaseEntity
{
    public Guid TemplateId { get; set; }
    public DocumentTemplate Template { get; set; } = default!;

    public DocumentBlockKind Kind { get; set; }

    /// <summary>Текст блока с плейсхолдерами вида {{FullName}}, {{PositionTitle}}
    /// и т.п. — подставляются DocumentPlaceholderResolver перед рендерингом
    /// (полный список поддерживаемых плейсхолдеров — там же).</summary>
    public string Text { get; set; } = default!;

    public bool Bold { get; set; }
    public int OrderIndex { get; set; }
}

public enum DocumentBlockKind
{
    Title,
    Paragraph,

    /// <summary>Строка вида "Поле: значение" — рендерится отдельной строкой,
    /// чтобы анкетные данные не сливались с описательным текстом.</summary>
    FieldLine,

    SignatureLine,
}
