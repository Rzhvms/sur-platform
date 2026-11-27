using System.Globalization;
using System.Text;
using System.Text.Json;
using Application.Constants;
using Application.Exceptions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Domain.DalModels;

namespace Infrastructure.Services.Documents;

/// <summary>Формирует CSV, Excel и административный JSON порциями сохранённого снимка</summary>
internal static class DocumentFormatter
{
    /// <summary>Название листа отчёта</summary>
    private const string SheetName = "Реестр рисков";

    /// <summary>Тип строки реестра</summary>
    private const string RiskRow = "Риск";

    /// <summary>Тип строки оценки</summary>
    private const string AssessmentRow = "Оценка";

    /// <summary>Тип строки меры</summary>
    private const string TreatmentRow = "Мера";

    /// <summary>Разделитель имён оргсвязей в одной ячейке</summary>
    private const string OrganizationSeparator = " / ";

    /// <summary>Разделитель нескольких связей</summary>
    private const string LinkSeparator = "; ";

    /// <summary>Столбцы табличного отчёта</summary>
    private static readonly string[] Columns =
        ["Тип строки", "Риск ID", "Запись ID", "Название", "Описание", "Статус", "Опасность", "Категория",
         "Объект", "Процессы и дирекции", "Оценка", "Уровень вероятности ID", "Уровень влияния ID",
         "Срок меры", "Стоимость план", "Стоимость факт"];

    /// <summary>Записывает сохранённый снимок в согласованном формате</summary>
    /// <param name="destination">Буфер готового файла</param>
    /// <param name="format">Запрошенный формат</param>
    /// <param name="job">Метаданные сформированного снимка</param>
    /// <param name="asOf">Момент среза</param>
    /// <param name="rows">Упорядоченный поток строк</param>
    /// <param name="cancellation">Отмена фоновой операции</param>
    internal static async Task WriteAsync(Stream destination, string format, DocumentJobDal job, DateTime asOf,
        IAsyncEnumerable<DocumentRowDal> rows, CancellationToken cancellation)
    {
        if (format == DocumentRules.Json)
        {
            using var writer = new Utf8JsonWriter(destination);
            writer.WriteStartArray();
            await foreach (var row in rows.WithCancellation(cancellation))
            {
                using var json = JsonDocument.Parse(row.Data);
                json.RootElement.WriteTo(writer);
                if (writer.BytesCommitted + writer.BytesPending > DocumentRules.MaxOutputBytes)
                {
                    throw new IdentityCommandException(400, DocumentRules.Invalid);
                }
                if (row.Number % DocumentRules.BatchSize == 0)
                {
                    await writer.FlushAsync(cancellation);
                }
            }
            writer.WriteEndArray();
            await writer.FlushAsync(cancellation);
            return;
        }

        string[] metadata =
        [
            "Автор отчёта", job.OwnerId.ToString(),
            "Момент среза UTC", asOf.ToString("O", CultureInfo.InvariantCulture),
            "Время формирования UTC", job.GeneratedAt?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty
        ];
        if (format == DocumentRules.Csv)
        {
            await using var writer = new StreamWriter(destination, new UTF8Encoding(true), leaveOpen: true);
            await WriteCsvRowAsync(writer, metadata, cancellation);
            await WriteCsvRowAsync(writer, Columns, cancellation);
            await foreach (var row in rows.WithCancellation(cancellation))
            {
                foreach (var cells in ReportCells(row.Data))
                {
                    await WriteCsvRowAsync(writer, cells, cancellation);
                }
                if (row.Number % DocumentRules.BatchSize == 0)
                {
                    await writer.FlushAsync(cancellation);
                    VerifyOutputSize(destination);
                }
            }

            return;
        }

        using var book = SpreadsheetDocument.Create(destination, SpreadsheetDocumentType.Workbook);
        var workbook = book.AddWorkbookPart();
        workbook.Workbook = new Workbook();
        var sheet = workbook.AddNewPart<WorksheetPart>();
        workbook.Workbook.AppendChild(new Sheets()).Append(new Sheet
        {
            Id = workbook.GetIdOfPart(sheet), SheetId = 1, Name = SheetName
        });
        using (var writer = OpenXmlWriter.Create(sheet))
        {
            writer.WriteStartElement(new Worksheet());
            writer.WriteStartElement(new SheetData());
            WriteExcelRow(writer, metadata);
            WriteExcelRow(writer, Columns);
            long characters = 0;
            await foreach (var row in rows.WithCancellation(cancellation))
            {
                foreach (var cells in ReportCells(row.Data))
                {
                    characters += cells.Sum(cell => (long)cell.Length);
                    if (characters > DocumentRules.MaxOutputBytes)
                    {
                        throw new IdentityCommandException(400, DocumentRules.Invalid);
                    }
                    WriteExcelRow(writer, cells, numericColumns: true);
                }
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
        }
        workbook.Workbook.Save();
    }

    /// <summary>Отклоняет чрезмерный размер результата до сохранения</summary>
    /// <param name="stream">Буфер результата</param>
    internal static void VerifyOutputSize(Stream stream)
    {
        if (stream.Length > DocumentRules.MaxOutputBytes)
        {
            throw new IdentityCommandException(400, DocumentRules.Invalid);
        }
    }

    /// <summary>Создаёт строку риска и отдельные строки его оценок и мер</summary>
    /// <param name="data">Материализованный агрегат</param>
    /// <returns>Табличные значения</returns>
    private static IEnumerable<string[]> ReportCells(string data)
    {
        using var json = JsonDocument.Parse(data);
        var snapshot = json.RootElement;
        var risk = snapshot.GetProperty(nameof(RiskDal));
        var id = Value(risk, nameof(RiskDal.Id));
        var processes = snapshot.GetProperty(nameof(ProcessDal)).EnumerateArray().Select(item =>
            Value(item, nameof(ProcessDal)) + OrganizationSeparator + Value(item, nameof(DirectorateDal)));
        yield return [RiskRow, id, id, Value(risk, nameof(RiskDal.Name)), Value(risk, nameof(RiskDal.Description)),
            Value(risk, nameof(RiskDal.Status)), Value(snapshot, nameof(HazardDal)), Value(snapshot, nameof(RiskCategoryDal)),
            Value(snapshot, nameof(AssetDal)), string.Join(LinkSeparator, processes), "", "", "", "", "", ""];
        foreach (var assessment in snapshot.GetProperty(nameof(RiskAssessmentDal)).EnumerateArray())
        {
            yield return [AssessmentRow, id, Value(assessment, nameof(RiskAssessmentDal.Id)), "", "",
                Value(assessment, nameof(RiskAssessmentDal.IsActive)), "", "", "", "",
                Value(assessment, nameof(RiskAssessmentDal.RiskScore)), Value(assessment, nameof(RiskAssessmentDal.LikelihoodLevelId)),
                Value(assessment, nameof(RiskAssessmentDal.ImpactLevelId)), "", "", ""];
        }
        foreach (var treatment in snapshot.GetProperty(nameof(TreatmentDal)).EnumerateArray())
        {
            yield return [TreatmentRow, id, Value(treatment, nameof(TreatmentDal.Id)), Value(treatment, nameof(TreatmentDal.Title)),
                Value(treatment, nameof(TreatmentDal.Description)), Value(treatment, nameof(TreatmentDal.Status)),
                "", "", "", "", "", "", "", Value(treatment, nameof(TreatmentDal.DueDate)),
                Value(treatment, nameof(TreatmentDal.CostPlanned)), Value(treatment, nameof(TreatmentDal.CostActual))];
        }
    }

    /// <summary>Преобразует скаляр сохранённой строки в значение ячейки</summary>
    /// <param name="row">Объект отчёта</param>
    /// <param name="name">Имя сохранённого поля</param>
    /// <returns>Текст ячейки</returns>
    private static string Value(JsonElement row, string name)
    {
        return row.TryGetProperty(name, out var value) ? value.ToString() : string.Empty;
    }

    /// <summary>Записывает строку CSV с защитой от интерпретации текста как формулы</summary>
    /// <param name="writer">Текстовый буфер файла</param>
    /// <param name="cells">Текстовые ячейки</param>
    /// <param name="cancellation">Отмена записи</param>
    private static async Task WriteCsvRowAsync(StreamWriter writer, string[] cells, CancellationToken cancellation)
    {
        const string quote = "\"";
        const string doubledQuote = "\"\"";
        const string textMarker = "'";
        var line = string.Join(DocumentRules.DefaultDelimiter, cells.Select(cell =>
        {
            var text = cell.AsSpan().TrimStart();
            if (cell.Length > 0 && (cell[0] is '\t' or '\r' or '\n' ||
                !text.IsEmpty && text[0] is ('=' or '+' or '-' or '@' or '＝' or '＋' or '－' or '＠')))
            {
                cell = textMarker + cell;
            }
            return quote + cell.Replace(quote, doubledQuote, StringComparison.Ordinal) + quote;
        }));
        await writer.WriteLineAsync(line.AsMemory(), cancellation);
    }

    /// <summary>Записывает типизированные текстовые ячейки без формул</summary>
    /// <param name="writer">Поток листа Excel</param>
    /// <param name="cells">Текстовые значения</param>
    /// <param name="numericColumns">Выводит рассчитанные показатели числовыми ячейками</param>
    private static void WriteExcelRow(OpenXmlWriter writer, string[] cells, bool numericColumns = false)
    {
        const int maxCellCharacters = 32767;
        writer.WriteStartElement(new Row());
        const int scoreColumn = 10;
        const int plannedCostColumn = 14;
        const int actualCostColumn = 15;
        for (var index = 0; index < cells.Length; index++)
        {
            var value = cells[index];
            if (numericColumns && index is (scoreColumn or plannedCostColumn or actualCostColumn) &&
                decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
            {
                writer.WriteElement(new Cell
                {
                    DataType = CellValues.Number,
                    CellValue = new CellValue(number.ToString(CultureInfo.InvariantCulture))
                });
                continue;
            }
            if (value.Length > maxCellCharacters)
            {
                throw new IdentityCommandException(400, DocumentRules.Invalid);
            }
            writer.WriteElement(new Cell
            {
                DataType = CellValues.InlineString,
                InlineString = new InlineString(new Text(value) { Space = SpaceProcessingModeValues.Preserve })
            });
        }
        writer.WriteEndElement();
    }
}
