using Application.UseCases.Treatments.Dto.Request;
using Application.UseCases.Risks.Dto.Request;
using System.Text.Json;
using Application.Constants;
using Application.Models;
using Application.UseCases.Risks.Dto;
using Domain.DalModels;
using Domain.DalModels.Risks;
using Domain.DalModels.Treatments;

namespace Application.Ports.Repositories;

/// <summary>Меры обработки риска, согласования и назначения исполнителей</summary>
public interface ITreatmentRepository
{
    /// <summary>Читает страницу общего списка мер текущего клиента</summary>
    /// <param name="actor">Участник клиентского запроса</param>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Меры, связанные названия и наличие следующей страницы</returns>
    Task<TreatmentRegistry> ListTreatmentRegistryAsync(RiskActor actor, int offset, int limit);

    /// <summary>Читает меры риска</summary>
    /// <param name="actor">Участник клиентского запроса</param>
    /// <param name="riskId">Риск</param>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Результат операции</returns>
    Task<TreatmentDal[]> ListTreatmentsAsync(RiskActor actor, Guid riskId, int offset, int limit);

    /// <summary>Читает карточку меры</summary>
    /// <param name="actor">Участник клиентского запроса</param>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <returns>Результат операции</returns>
    Task<TreatmentCard> GetTreatmentAsync(RiskActor actor, Guid id);

    /// <summary>Создаёт или сохраняет черновик</summary>
    /// <param name="actor">Участник клиентского запроса</param>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="create">Создание новой меры</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    Task<TreatmentDal> SaveTreatmentDraftAsync(RiskActor actor, Guid id, bool create, TreatmentDraftRequest request);

    /// <summary>Выполняет ручной переход</summary>
    /// <param name="actor">Участник клиентского запроса</param>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="target">Целевой статус из маршрута</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    Task<TreatmentDal> TransitionTreatmentAsync(RiskActor actor, Guid id, string target, TreatmentActionRequest request);

    /// <summary>Запускает весь ожидающий набор</summary>
    /// <param name="actor">Участник клиентского запроса</param>
    /// <param name="riskId">Риск</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    Task<RiskDal> StartTreatmentCycleAsync(RiskActor actor, Guid riskId, TreatmentActionRequest request);

    /// <summary>Назначает исполнителя или передаёт меру к выполнению</summary>
    /// <param name="actor">Участник клиентского запроса</param>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="dispatch">Атомарный переход к ожиданию</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    Task<TreatmentDal> AssignTreatmentAsync(RiskActor actor, Guid id, bool dispatch, TreatmentActionRequest request);

    /// <summary>Читает допустимых исполнителей</summary>
    /// <param name="actor">Участник клиентского запроса</param>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Результат операции</returns>
    Task<TreatmentExecutorOption[]> ListTreatmentExecutorsAsync(RiskActor actor, Guid id, int offset, int limit);

    /// <summary>Отправляет меру на согласование</summary>
    /// <param name="actor">Участник клиентского запроса</param>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    Task<TreatmentApprovalDal> SubmitTreatmentAsync(RiskActor actor, Guid id, TreatmentActionRequest request);

    /// <summary>Принимает решение по запросу</summary>
    /// <param name="actor">Участник клиентского запроса</param>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="decision">Решение из маршрута</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    Task<TreatmentApprovalDal> DecideTreatmentApprovalAsync(RiskActor actor, Guid id, string decision, TreatmentActionRequest request);

    /// <summary>Читает очередь и её счётчик</summary>
    /// <param name="actor">Участник клиентского запроса</param>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Результат операции</returns>
    Task<TreatmentApprovalQueue> ListTreatmentApprovalsAsync(RiskActor actor, int offset, int limit);

    /// <summary>Читает пороги согласования</summary>
    /// <param name="actor">Участник клиентского запроса</param>
    /// <returns>Результат операции</returns>
    Task<TreatmentApprovalSettingsDal> GetTreatmentSettingsAsync(RiskActor actor);

    /// <summary>Сохраняет пороги согласования</summary>
    /// <param name="actor">Участник клиентского запроса</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    Task<TreatmentApprovalSettingsDal> SaveTreatmentSettingsAsync(RiskActor actor, TreatmentThresholdsRequest request);

    /// <summary>Читает комментарии меры</summary>
    /// <param name="actor">Участник клиентского запроса</param>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Результат операции</returns>
    Task<TreatmentCommentDal[]> ListTreatmentCommentsAsync(RiskActor actor, Guid id, int offset, int limit);

    /// <summary>Создаёт, редактирует или удаляет комментарий</summary>
    /// <param name="actor">Участник клиентского запроса</param>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="commentId">Идентификатор изменяемого комментария</param>
    /// <param name="request">Данные команды</param>
    /// <param name="delete">Удаление комментария</param>
    /// <returns>Результат операции</returns>
    Task<TreatmentCommentDal> SaveTreatmentCommentAsync(RiskActor actor, Guid id, Guid? commentId, TreatmentCommentRequest request, bool delete);

    /// <summary>Читает историю запросов согласования меры</summary>
    /// <param name="actor">Пользователь текущего клиента</param>
    /// <param name="id">Мера</param>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Запросы с исходным решением и отзывом</returns>
    Task<TreatmentApprovalDal[]> ListTreatmentApprovalHistoryAsync(RiskActor actor, Guid id, int offset, int limit);
}
