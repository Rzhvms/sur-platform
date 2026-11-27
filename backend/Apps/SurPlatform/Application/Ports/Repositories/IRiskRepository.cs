using Application.UseCases.Treatments.Dto.Request;
using Application.UseCases.Risks.Dto.Request;
using System.Text.Json;
using Application.Constants;
using Application.Models;
using Application.UseCases.Risks.Dto;
using Domain.DalModels;

namespace Application.Ports.Repositories;

/// <summary>Прикладные операции RiskRepository в выбранной клиентской базе</summary>
public interface IRiskRepository
{
    /// <summary>Список рисков по фильтрам</summary>
    /// <param name="actor">Пользователь из проверенной сессии</param>
    /// <param name="filter">Отбор и пагинация</param>
    /// <returns>Результат в клиентском контексте</returns>
    Task<RiskRow[]> ListAsync(RiskActor actor, RiskListQuery filter);

    /// <summary>Шесть колонок канбана</summary>
    /// <param name="actor">Пользователь из проверенной сессии</param>
    /// <param name="filter">Отбор и пагинация</param>
    /// <returns>Результат в клиентском контексте</returns>
    Task<RiskColumn[]> BoardAsync(RiskActor actor, RiskListQuery filter);

    /// <summary>Карточка риска</summary>
    /// <param name="actor">Пользователь из проверенной сессии</param>
    /// <param name="id">Идентификатор записи</param>
    /// <returns>Результат в клиентском контексте</returns>
    Task<RiskCard> GetAsync(RiskActor actor, Guid id);

    /// <summary>Новый черновик риска</summary>
    /// <param name="actor">Пользователь из проверенной сессии</param>
    /// <param name="request">Поля команды</param>
    /// <returns>Результат в клиентском контексте</returns>
    Task<RiskDal> CreateAsync(RiskActor actor, RiskPassportRequest request);

    /// <summary>Редактирование паспорта до первой оценки</summary>
    /// <param name="actor">Пользователь из проверенной сессии</param>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Поля команды</param>
    /// <returns>Результат в клиентском контексте</returns>
    Task<RiskDal> UpdateAsync(RiskActor actor, Guid id, RiskPassportRequest request);

    /// <summary>Закрытие или возврат на переоценку</summary>
    /// <param name="actor">Пользователь из проверенной сессии</param>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Поля команды</param>
    /// <param name="close">Закрытие вместо возврата</param>
    /// <returns>Результат в клиентском контексте</returns>
    Task<RiskDal> TransitionAsync(RiskActor actor, Guid id, RiskTransitionRequest request, bool close);

    /// <summary>Видимые комментарии риска</summary>
    /// <param name="actor">Пользователь из проверенной сессии</param>
    /// <param name="riskId">Родительский риск</param>
    /// <param name="offset">Смещение</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Результат в клиентском контексте</returns>
    Task<RiskCommentDal[]> ListCommentsAsync(RiskActor actor, Guid riskId, int offset, int limit);

    /// <summary>Создание или редактирование комментария</summary>
    /// <param name="actor">Пользователь из проверенной сессии</param>
    /// <param name="riskId">Родительский риск</param>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Поля команды</param>
    /// <returns>Результат в клиентском контексте</returns>
    Task<RiskCommentDal> SaveCommentAsync(RiskActor actor, Guid riskId, Guid? id, RiskCommentRequest request);

    /// <summary>Логическое удаление комментария</summary>
    /// <param name="actor">Пользователь из проверенной сессии</param>
    /// <param name="riskId">Родительский риск</param>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="version">Ожидаемая версия</param>
    Task DeleteCommentAsync(RiskActor actor, Guid riskId, Guid id, Guid version);

    /// <summary>Читает варианты выбора для паспорта риска</summary>
    /// <param name="actor">Пользователь текущей клиентской базы</param>
    /// <param name="offset">Смещение</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Допустимые строки справочника</returns>
    Task<RiskProcessOption[]> ListProcessesAsync(RiskActor actor, int offset, int limit);

    /// <summary>Читает варианты выбора для паспорта риска</summary>
    /// <param name="actor">Пользователь текущей клиентской базы</param>
    /// <param name="offset">Смещение</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Допустимые строки справочника</returns>
    Task<AssetDal[]> ListAssetsAsync(RiskActor actor, int offset, int limit);

    /// <summary>Фиксирует единственный способ работы в цикле</summary>
    /// <param name="actor">Пользователь текущего клиента</param>
    /// <param name="id">Риск</param>
    /// <param name="request">Версия карточки и параметры выбора</param>
    /// <param name="mode">Режим из маршрута</param>
    /// <returns>Результат операции</returns>
    Task<RiskDal> ChooseDecisionAsync(RiskActor actor, Guid id, RiskDecisionRequest request, string mode);

    /// <summary>Читает допустимые процессы для разделения риска</summary>
    /// <param name="actor">Инициатор</param>
    /// <param name="id">Риск</param>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Действующие процессы клиента</returns>
    Task<RiskProcessOption[]> ListSplitProcessesAsync(RiskActor actor, Guid id, int offset, int limit);

    /// <summary>Читает доступные новые процессы для передачи</summary>
    /// <param name="actor">Текущий пользователь</param>
    /// <param name="id">Риск</param>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Страница допустимых процессов</returns>
    Task<RiskProcessOption[]> ListTransferProcessesAsync(RiskActor actor, Guid id, int offset, int limit);
}
