using System.Globalization;
using ClosedXML.Excel;

namespace TibbiNav.Application.BulkImport;

/// <summary>Результат разбора XLSX: заголовки первой строки первого листа + строки данных
/// (заголовок → текстовое значение ячейки, пустые ячейки → "").</summary>
public sealed record ParsedWorkbook(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyDictionary<string, string>> Rows);

/// <summary>
/// Раздел 63: читает первый лист XLSX-файла. Первая непустая строка — заголовки
/// столбцов, дальше — данные до первой полностью пустой строки. Значения ячеек
/// читаются как отображаемый текст (даты/числа приводятся к строке в
/// культурно-нейтральном формате), дальнейшую типизацию делает
/// IBulkImportDefinition.ValidateRowAsync под конкретный целевой формат.
/// </summary>
public static class XlsxParser
{
    /// <summary>Если `preferredSheetName` задан и такой лист есть в файле —
    /// читаем его (так шаблон с несколькими листами, напр. наш собственный
    /// образец, разбирается правильно вне зависимости от порядка листов);
    /// иначе — первый лист (типичный экспорт из 1С/Excel с одним листом).</summary>
    public static ParsedWorkbook Parse(Stream fileStream, string? preferredSheetName = null)
    {
        using var workbook = new XLWorkbook(fileStream);
        var sheet = (preferredSheetName is not null
            ? workbook.Worksheets.FirstOrDefault(w => string.Equals(w.Name, preferredSheetName, StringComparison.OrdinalIgnoreCase))
            : null) ?? workbook.Worksheets.First();
        var usedRange = sheet.RangeUsed()
            ?? throw new InvalidOperationException("Лист пуст.");

        var rows = usedRange.RowsUsed().ToList();
        if (rows.Count == 0)
            throw new InvalidOperationException("В файле нет строк с данными.");

        var headerRow = rows[0];
        var headers = headerRow.Cells()
            .Select(c => c.GetString().Trim())
            .ToList();

        if (headers.Count == 0 || headers.All(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Не найдена строка заголовков.");

        var dataRows = new List<IReadOnlyDictionary<string, string>>();
        foreach (var row in rows.Skip(1))
        {
            var cells = row.Cells(1, headers.Count).ToList();
            if (cells.All(c => string.IsNullOrWhiteSpace(c.GetString())))
                continue; // пропускаем полностью пустые строки (напр. хвост файла)

            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
            {
                var header = headers[i];
                if (string.IsNullOrWhiteSpace(header)) continue;
                var cell = i < cells.Count ? cells[i] : null;
                dict[header] = cell is null ? string.Empty : CellToInvariantString(cell);
            }
            dataRows.Add(dict);
        }

        return new ParsedWorkbook(headers.Where(h => !string.IsNullOrWhiteSpace(h)).ToList(), dataRows);
    }

    /// <summary>Отдаёт значение ячейки в формате, не зависящем от локали Excel-файла
    /// (даты — ISO yyyy-MM-dd, числа — InvariantCulture) — иначе результат
    /// GetString() зависит от регионального формата, с которым файл сохраняли.</summary>
    private static string CellToInvariantString(IXLCell cell)
    {
        if (cell.DataType == XLDataType.DateTime)
            return cell.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (cell.DataType == XLDataType.Number)
            return cell.GetDouble().ToString(CultureInfo.InvariantCulture);

        return cell.GetString().Trim();
    }
}
