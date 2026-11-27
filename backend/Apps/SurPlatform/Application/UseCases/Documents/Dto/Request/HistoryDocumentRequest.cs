using Application.UseCases.Risks.Dto;

namespace Application.UseCases.Documents.Dto.Request;

/// <summary>Идемпотентный запрос административной выгрузки</summary>
/// <param name="OperationId">Ключ повторяемой команды</param>
/// <param name="Period">Включительный период UTC</param>
public sealed record HistoryDocumentRequest(Guid OperationId, BusinessPeriod Period);
