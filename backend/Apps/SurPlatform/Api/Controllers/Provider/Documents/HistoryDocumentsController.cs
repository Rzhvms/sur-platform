using Api.Extensions;
using Application.UseCases.Documents;
using Application.UseCases.Documents.Dto.Request;
using Application.UseCases.Documents.Dto.Response;
using Core.Api.Controllers.ControllerTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Provider.Documents;

/// <summary>Документы административной истории</summary>
/// <param name="useCases">Прикладные операции документов</param>
[Authorize]
[Route(HistoryRoute)]
public sealed class HistoryDocumentsController(DocumentUseCaseManager useCases) : BaseProviderController
{
    /// <summary>Новый контракт формирования JSON по устойчивому ключу</summary>
    private const string HistoryRoute = "documents/history";

    /// <summary>Регистрирует согласованный административный снимок</summary>
    /// <param name="request">Ключ операции и включительный диапазон UTC</param>
    /// <returns>Состояние задания JSON</returns>
    [HttpPost]
    [ProducesResponseType<DocumentStatus>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public Task<DocumentStatus> RequestAsync(HistoryDocumentRequest request)
    {
        return useCases.RequestHistoryAsync(User.GetRiskActor(), request);
    }
}
