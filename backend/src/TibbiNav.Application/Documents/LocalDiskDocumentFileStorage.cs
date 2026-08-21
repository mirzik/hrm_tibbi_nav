namespace TibbiNav.Application.Documents;

/// <summary>Локальная dev-реализация IDocumentFileStorage — до появления
/// настоящего S3-compatible хранилища (раздел 80). rootPath передаётся из
/// Api-слоя (Program.cs) из конфигурации, чтобы Application не зависел от
/// ASP.NET-специфичного IConfiguration.</summary>
public sealed class LocalDiskDocumentFileStorage(string rootPath) : IDocumentFileStorage
{
    public async Task SaveAsync(string key, byte[] content, CancellationToken ct)
    {
        var path = ToPath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, content, ct);
    }

    public async Task<byte[]> ReadAsync(string key, CancellationToken ct)
    {
        var path = ToPath(key);
        if (!File.Exists(path))
            throw new FileNotFoundException("Файл документа не найден в хранилище.", path);
        return await File.ReadAllBytesAsync(path, ct);
    }

    private string ToPath(string key) => Path.Combine(rootPath, key.Replace('/', Path.DirectorySeparatorChar));
}
