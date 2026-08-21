using TibbiNav.Domain.Documents;

namespace TibbiNav.Application.Documents;

/// <summary>Блок шаблона после подстановки плейсхолдеров — общий вход для
/// DocxDocumentRenderer и PdfDocumentRenderer, поэтому оба формата рендерятся
/// из одного и того же уже готового текста и не могут разойтись между собой.</summary>
public sealed record ResolvedDocumentBlock(DocumentBlockKind Kind, string Text, bool Bold);
