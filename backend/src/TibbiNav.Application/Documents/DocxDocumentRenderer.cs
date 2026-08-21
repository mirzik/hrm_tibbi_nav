using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TibbiNav.Domain.Documents;

namespace TibbiNav.Application.Documents;

/// <summary>Раздел 29: рендерит уже подставленные блоки шаблона в .docx через
/// DocumentFormat.OpenXml (уже используется проектом транзитивно через
/// ClosedXML — отдельного пакета не потребовалось). Минимальное форматирование
/// (заголовок по центру жирным, обычные абзацы, строки полей) — достаточно
/// для юридически читаемого документа, не притворяется полноценным
/// WYSIWYG-редактором шаблонов.</summary>
public sealed class DocxDocumentRenderer
{
    public byte[] Render(IReadOnlyList<ResolvedDocumentBlock> blocks)
    {
        using var stream = new MemoryStream();

        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = new Body();

            foreach (var block in blocks)
                body.Append(BuildParagraph(block));

            mainPart.Document.Append(body);
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static Paragraph BuildParagraph(ResolvedDocumentBlock block)
    {
        var paragraph = new Paragraph();
        var paragraphProps = new ParagraphProperties();

        if (block.Kind == DocumentBlockKind.Title)
            paragraphProps.Justification = new Justification { Val = JustificationValues.Center };
        if (block.Kind == DocumentBlockKind.SignatureLine)
            paragraphProps.SpacingBetweenLines = new SpacingBetweenLines { Before = "480" }; // отступ перед подписью

        paragraph.Append(paragraphProps);

        var run = new Run();
        var runProps = new RunProperties();
        if (block.Bold || block.Kind == DocumentBlockKind.Title)
            runProps.Append(new Bold());
        if (block.Kind == DocumentBlockKind.Title)
            runProps.Append(new FontSize { Val = "32" }); // 16pt (полуторные единицы OOXML)

        run.Append(runProps);
        run.Append(new Text(block.Text) { Space = SpaceProcessingModeValues.Preserve });
        paragraph.Append(run);

        return paragraph;
    }
}
