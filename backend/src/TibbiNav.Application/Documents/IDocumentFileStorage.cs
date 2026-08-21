namespace TibbiNav.Application.Documents;

/// <summary>
/// Раздел 80: абстракция над файловым хранилищем сгенерированных документов.
/// Домен (EmployeeDocument) хранит только ключ (DocxFileKey/PdfFileKey) — это
/// сделано намеренно, чтобы LocalDiskDocumentFileStorage (см. ниже) можно было
/// заменить на S3-compatible реализацию без изменения Domain/Application кода.
/// </summary>
public interface IDocumentFileStorage
{
    Task SaveAsync(string key, byte[] content, CancellationToken ct);
    Task<byte[]> ReadAsync(string key, CancellationToken ct);
}
