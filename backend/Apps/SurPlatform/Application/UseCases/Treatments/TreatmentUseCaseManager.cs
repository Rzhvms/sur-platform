using Application.Models;
using Application.Ports.Repositories;
using Application.UseCases.Treatments.Dto.Request;
using Domain.DalModels;

namespace Application.UseCases.Treatments;

/// <summary>Прикладные операции мер, исполнителей и согласований</summary>
/// <param name="repository">Хранилище рисков текущего клиента</param>
public sealed class TreatmentUseCaseManager(IRiskRepository repository)
{
    /// <summary>Читает меры риска</summary>
    /// <param name="actor">Участник клиентского запроса</param>
    /// <param name="riskId">Риск</param>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Результат операции</returns>
    public Task<TreatmentDal[]> ListTreatmentsAsync(RiskActor actor, Guid riskId, int offset, int limit)
    {
        return repository.ListTreatmentsAsync(actor, riskId, offset, limit);
    }

    /// <summary>Читает карточку меры</summary>
    /// <param name="actor">Участник клиентского запроса</param>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <returns>Результат операции</returns>
    public Task<TreatmentCard> GetTreatmentAsync(RiskActor actor, Guid id)
    {
        return repository.GetTreatmentAsync(actor, id);
    }

    /// <summary>Создаёт или сохраняет черновик</summary>
    /// <param name="actor">Участник клиентского запроса</param>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="create">Создание новой меры</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    public Task<TreatmentDal> SaveTreatmentDraftAsync(RiskActor actor, Guid id, bool create, TreatmentDraftRequest request)
    {
        return repository.SaveTreatmentDraftAsync(actor, id, create, request);
    }

    /// <summary>Выполняет ручной переход</summary>
    /// <param name="actor">Участник клиентского запроса</param>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="target">Целевой статус из маршрута</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    public Task<TreatmentDal> TransitionTreatmentAsync(RiskActor actor, Guid id, string target, TreatmentActionRequest request)
    {
        return repository.TransitionTreatmentAsync(actor, id, target, request);
    }

    /// <summary>Запускает весь ожидающий набор</summary>
    /// <param name="actor">Участник клиентского запроса</param>
    /// <param name="riskId">Риск</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    public Task<RiskDal> StartTreatmentCycleAsync(RiskActor actor, Guid riskId, TreatmentActionRequest request)
    {
        return repository.StartTreatmentCycleAsync(actor, riskId, request);
    }

    /// <summary>Назначает исполнителя или передаёт меру к выполнению</summary>
    /// <param name="actor">Участник клиентского запроса</param>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="dispatch">Атомарный переход к ожиданию</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    public Task<TreatmentDal> AssignTreatmentAsync(RiskActor actor, Guid id, bool dispatch, TreatmentActionRequest request)
    {
        return repository.AssignTreatmentAsync(actor, id, dispatch, request);
    }

    /// <summary>Читает допустимых исполнителей</summary>
    /// <param name="actor">Участник клиентского запроса</param>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Результат операции</returns>
    public Task<TreatmentExecutorOption[]> ListTreatmentExecutorsAsync(RiskActor actor, Guid id, int offset, int limit)
    {
        return repository.ListTreatmentExecutorsAsync(actor, id, offset, limit);
    }

    /// <summary>Отправляет меру на согласование</summary>
    /// <param name="actor">Участник клиентского запроса</param>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    public Task<TreatmentApprovalDal> SubmitTreatmentAsync(RiskActor actor, Guid id, TreatmentActionRequest request)
    {
        return repository.SubmitTreatmentAsync(actor, id, request);
    }

    /// <summary>Принимает решение по запросу</summary>
    /// <param name="actor">Участник клиентского запроса</param>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="decision">Решение из маршрута</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    public Task<TreatmentApprovalDal> DecideTreatmentApprovalAsync(RiskActor actor, Guid id, string decision, TreatmentActionRequest request)
    {
        return repository.DecideTreatmentApprovalAsync(actor, id, decision, request);
    }

    /// <summary>Читает очередь и её счётчик</summary>
    /// <param name="actor">Участник клиентского запроса</param>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Результат операции</returns>
    public Task<TreatmentApprovalQueue> ListTreatmentApprovalsAsync(RiskActor actor, int offset, int limit)
    {
        return repository.ListTreatmentApprovalsAsync(actor, offset, limit);
    }

    /// <summary>Читает пороги согласования</summary>
    /// <param name="actor">Участник клиентского запроса</param>
    /// <returns>Результат операции</returns>
    public Task<TreatmentApprovalSettingsDal> GetTreatmentSettingsAsync(RiskActor actor)
    {
        return repository.GetTreatmentSettingsAsync(actor);
    }

    /// <summary>Сохраняет пороги согласования</summary>
    /// <param name="actor">Участник клиентского запроса</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    public Task<TreatmentApprovalSettingsDal> SaveTreatmentSettingsAsync(RiskActor actor, TreatmentThresholdsRequest request)
    {
        return repository.SaveTreatmentSettingsAsync(actor, request);
    }

    /// <summary>Читает комментарии меры</summary>
    /// <param name="actor">Участник клиентского запроса</param>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Результат операции</returns>
    public Task<TreatmentCommentDal[]> ListTreatmentCommentsAsync(RiskActor actor, Guid id, int offset, int limit)
    {
        return repository.ListTreatmentCommentsAsync(actor, id, offset, limit);
    }

    /// <summary>Создаёт, редактирует или удаляет комментарий</summary>
    /// <param name="actor">Участник клиентского запроса</param>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="commentId">Идентификатор изменяемого комментария</param>
    /// <param name="request">Данные команды</param>
    /// <param name="delete">Удаление комментария</param>
    /// <returns>Результат операции</returns>
    public Task<TreatmentCommentDal> SaveTreatmentCommentAsync(RiskActor actor, Guid id, Guid? commentId, TreatmentCommentRequest request, bool delete)
    {
        return repository.SaveTreatmentCommentAsync(actor, id, commentId, request, delete);
    }

    /// <summary>Читает историю запросов согласования меры</summary>
    /// <param name="actor">Пользователь текущего клиента</param>
    /// <param name="id">Мера</param>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Запросы с исходным решением и отзывом</returns>
    public Task<TreatmentApprovalDal[]> ListTreatmentApprovalHistoryAsync(RiskActor actor, Guid id, int offset, int limit)
    {
        return repository.ListTreatmentApprovalHistoryAsync(actor, id, offset, limit);
    }
}
