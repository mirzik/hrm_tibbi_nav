using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TibbiNav.Domain.Documents;

namespace TibbiNav.Application.Documents;

/// <summary>Раздел 29: рендерит те же блоки, что и DocxDocumentRenderer, в PDF
/// через QuestPDF (лицензия Community — бесплатна для этого проекта; тип
/// лицензии активируется один раз при старте, см. Program.cs). Сознательно
/// НЕ конвертирует уже собранный .docx в PDF (это потребовало бы LibreOffice/
/// MS Office на сервере) — оба формата рендерятся независимо из одной и той
/// же промежуточной модели блоков, поэтому не могут разойтись по содержанию.
/// Шрифт — "Noto Sans" (регистрируется как embedded resource в Program.cs) —
/// без явного указания QuestPDF/SkiaSharp может подобрать шрифт без кириллицы
/// на хосте (особенно в минимальных Linux-контейнерах) и молча потерять
/// русский текст.</summary>
public sealed class PdfDocumentRenderer
{
    private const string FontFamily = "Noto Sans";

    public byte[] Render(IReadOnlyList<ResolvedDocumentBlock> blocks)
    {
        var document = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontFamily(FontFamily).FontSize(11));

                page.Content().Column(column =>
                {
                    column.Spacing(8);
                    foreach (var block in blocks)
                        column.Item().Element(e => RenderBlock(e, block));
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void RenderBlock(QuestPDF.Infrastructure.IContainer container, ResolvedDocumentBlock block)
    {
        if (block.Kind == DocumentBlockKind.SignatureLine)
            container = container.PaddingTop(16);

        var alignedContainer = block.Kind == DocumentBlockKind.Title ? container.AlignCenter() : container;

        alignedContainer.Text(text =>
        {
            var span = text.Span(block.Text);
            if (block.Bold || block.Kind == DocumentBlockKind.Title)
                span.Bold();
            if (block.Kind == DocumentBlockKind.Title)
                span.FontSize(16);
        });
    }
}
