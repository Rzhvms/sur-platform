namespace Application.Constants;

/// <summary>Состояния заданий, форматы и ограничения документов</summary>
public static class DocumentRules
{
    /// <summary>Право просмотра отчёта</summary>
    public const string View = "view_report";

    /// <summary>Право формирования файла отчёта</summary>
    public const string Export = "export_report";

    /// <summary>Право импорта данных</summary>
    public const string ImportRight = "import_data";

    /// <summary>Срез реестра рисков</summary>
    public const string Report = "REPORT";

    /// <summary>Административная история</summary>
    public const string History = "HISTORY";

    /// <summary>Импорт риска</summary>
    public const string Import = "IMPORT";

    /// <summary>Ожидает загрузки CSV</summary>
    public const string Upload = "UPLOADING";

    /// <summary>Ожидает сопоставления столбцов</summary>
    public const string Mapping = "AWAITING_MAPPING";

    /// <summary>Ожидает обработки</summary>
    public const string Pending = "PENDING";

    /// <summary>Чтение или создание результата</summary>
    public const string Processing = "PROCESSING";

    /// <summary>Все данные подтверждены</summary>
    public const string Completed = "COMPLETED";

    /// <summary>Импорт завершён с ошибками строк</summary>
    public const string Partial = "COMPLETED_WITH_ERRORS";

    /// <summary>Обработка прервана</summary>
    public const string Failed = "FAILED";

    /// <summary>Выгрузка с разделителями</summary>
    public const string Csv = "CSV";

    /// <summary>Книга Excel</summary>
    public const string Xlsx = "XLSX";

    /// <summary>Административный массив событий</summary>
    public const string Json = "JSON";

    /// <summary>Разделитель CSV</summary>
    public const string DefaultDelimiter = ",";

    /// <summary>Альтернативный разделитель CSV</summary>
    public const string SemicolonDelimiter = ";";

    /// <summary>Некорректные параметры или содержимое</summary>
    public const string Invalid = "DOCUMENT_INVALID";

    /// <summary>Задание или файл недоступны</summary>
    public const string Missing = "DOCUMENT_NOT_FOUND";

    /// <summary>Ключ переиспользован для другой операции или неверное состояние</summary>
    public const string Conflict = "DOCUMENT_CONFLICT";

    /// <summary>Ошибка хранения или формирования</summary>
    public const string Unavailable = "DOCUMENT_UNAVAILABLE";

    /// <summary>Строку нельзя преобразовать в паспорт риска</summary>
    public const string InvalidRow = "IMPORT_INVALID_ROW";

    /// <summary>CSV невозможно продолжить читать</summary>
    public const string InvalidCsv = "IMPORT_INVALID_CSV";

    /// <summary>HTTP тип исходного потока</summary>
    public const string BinaryMediaType = "application/octet-stream";

    /// <summary>HTTP тип файла CSV</summary>
    public const string CsvMediaType = "text/csv; charset=utf-8";

    /// <summary>HTTP тип файла Excel</summary>
    public const string ExcelMediaType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>HTTP тип файла журнала</summary>
    public const string JsonMediaType = "application/json";

    /// <summary>Верхняя граница размера входного CSV</summary>
    public const int MaxImportBytes = 8 * 1024 * 1024;

    /// <summary>Верхняя граница сформированного файла</summary>
    public const int MaxOutputBytes = 64 * 1024 * 1024;

    /// <summary>Длина hex представления SHA-256</summary>
    public const int DigestLength = 64;

    /// <summary>Количество строк на одну порцию</summary>
    public const int BatchSize = 128;

    /// <summary>Предельное количество строк в задании</summary>
    public const int RowLimit = 20000;
}
