using ClosedXML.Excel;

namespace TibbiNav.Application.BulkImport;

/// <summary>
/// Раздел 63: генерирует образец XLSX-шаблона для Bulk Import — по листу на
/// каждый ImportKind (заголовки берутся из IBulkImportDefinition.Fields, чтобы
/// шаблон никогда не расходился с реальной валидацией) плюс лист "Справочник"
/// с допустимыми значениями enum-полей. Обязательные столбцы помечены "*".
/// </summary>
public sealed class BulkImportTemplateGenerator(IEnumerable<IBulkImportDefinition> definitions)
{
    public byte[] GenerateWorkbook()
    {
        using var workbook = new XLWorkbook();

        AddStaffingSheet(workbook);
        AddEmployeesSheet(workbook);
        AddReferenceSheet(workbook);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private void AddStaffingSheet(XLWorkbook workbook)
    {
        var def = definitions.First(d => d.Kind == Domain.BulkImport.ImportKind.StaffingSchedule);
        var sheet = workbook.Worksheets.Add(def.TemplateSheetName);
        WriteHeaders(sheet, def);

        WriteRow(sheet, 2, ["DUS", "Ресепшн", "Администратор", "Ресепшн", "1", "4500"]);
        WriteRow(sheet, 3, ["DUS", "Хирургия", "Врач-хирург", "Врач", "1", "12000"]);
        WriteRow(sheet, 4, ["KHJ", "Ресепшн", "Администратор", "Ресепшн", "0.5", "2200"]);

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();
    }

    private void AddEmployeesSheet(XLWorkbook workbook)
    {
        var def = definitions.First(d => d.Kind == Domain.BulkImport.ImportKind.Employees);
        var sheet = workbook.Worksheets.Add(def.TemplateSheetName);
        WriteHeaders(sheet, def);

        WriteRow(sheet, 2,
        [
            "DUS", "Ресепшн", "Администратор", "Иванова Мария Сергеевна", "Ж", "15.03.1990", "TJ",
            "+992900000001", "maria.personal@example.com", "maria@tibbinav.local",
            "01.02.2024", "Полная занятость", "1", "4500", "",
        ]);
        WriteRow(sheet, 3,
        [
            "DUS", "Хирургия", "Врач-хирург", "Петров Иван Александрович", "М", "22.07.1985", "TJ",
            "+992900000002", "ivan.personal@example.com", "ivan@tibbinav.local",
            "10.01.2023", "Полная занятость", "1", "12000", "",
        ]);
        WriteRow(sheet, 4,
        [
            "DUS", "Ресепшн", "Администратор", "Сидорова Анна Петровна", "Ж", "05.11.1995", "TJ",
            "", "", "",
            "01.06.2024", "Частичная занятость", "0.5", "2200", "Иванова Мария Сергеевна",
        ]);

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();
    }

    private static void AddReferenceSheet(XLWorkbook workbook)
    {
        var sheet = workbook.Worksheets.Add("Справочник");
        sheet.Cell(1, 1).Value = "Поле";
        sheet.Cell(1, 2).Value = "Допустимые значения";
        sheet.Range(1, 1, 1, 2).Style.Font.Bold = true;

        var row = 2;
        WriteReferenceList(sheet, ref row, "Категория персонала (лист «Штатное расписание»)", ImportLabelMaps.Category.Keys);
        WriteReferenceList(sheet, ref row, "Пол (лист «Сотрудники»)", ImportLabelMaps.Gender.Keys);
        WriteReferenceList(sheet, ref row, "Тип занятости (лист «Сотрудники»)", ImportLabelMaps.EmploymentType.Keys);

        sheet.Columns().AdjustToContents();
    }

    private static void WriteReferenceList(IXLWorksheet sheet, ref int row, string field, IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            sheet.Cell(row, 1).Value = field;
            sheet.Cell(row, 2).Value = value;
            row++;
        }
    }

    private static void WriteHeaders(IXLWorksheet sheet, IBulkImportDefinition def)
    {
        for (var i = 0; i < def.Fields.Count; i++)
        {
            var field = def.Fields[i];
            var cell = sheet.Cell(1, i + 1);
            cell.Value = field.Label + (field.Required ? " *" : "");
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F0FE");
        }
    }

    private static void WriteRow(IXLWorksheet sheet, int rowIndex, string[] values)
    {
        for (var i = 0; i < values.Length; i++)
            sheet.Cell(rowIndex, i + 1).Value = values[i];
    }
}
