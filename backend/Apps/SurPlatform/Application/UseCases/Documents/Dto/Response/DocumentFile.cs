namespace Application.UseCases.Documents.Dto.Response;

/// <summary>Подтверждённые метаданные для потоковой передачи</summary>
/// <param name="Name">Безопасное имя вложения</param>
/// <param name="ContentType">Тип файла</param>
/// <param name="Size">Подтверждённый размер</param>
/// <param name="Checksum">SHA-256 содержимого в hex</param>
public sealed record DocumentFile(string Name, string ContentType, long Size, string Checksum);
