using Application.UseCases.Treatments.Dto.Request;
using Application.UseCases.Risks.Dto.Request;
using System.Text.Json;
using Application.Constants;
using Application.Models;
using Application.UseCases.Risks.Dto;
using Domain.DalModels;

namespace Application.Ports.Repositories;

/// <summary>Операции реестра и истории клиента</summary>
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

    /// <summary>Административные справочники или доступные варианты выбора</summary>
    /// <param name="actor">Пользователь из проверенной сессии</param>
    /// <param name="kind">Тип справочника</param>
    /// <param name="available">Только допустимые для новых связей</param>
    /// <param name="offset">Смещение</param>
    /// <param name="limit">Размер страницы</param>
    /// <param name="search">Фрагмент названия</param>
    /// <returns>Результат в клиентском контексте</returns>
    Task<RiskReference[]> ListReferencesAsync(RiskActor actor, RiskReferenceKind kind, bool available, int offset, int limit, string? search);

    /// <summary>Изменение записи справочника</summary>
    /// <param name="actor">Пользователь из проверенной сессии</param>
    /// <param name="kind">Тип справочника</param>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Поля команды</param>
    /// <returns>Результат в клиентском контексте</returns>
    Task<RiskReference> SaveReferenceAsync(RiskActor actor, RiskReferenceKind kind, Guid? id, RiskReferenceRequest request);

    /// <summary>Страница административной истории по датам UTC</summary>
    /// <param name="actor">Пользователь из проверенной сессии</param>
    /// <param name="period">Включительный диапазон дат UTC</param>
    /// <param name="offset">Смещение</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Результат в клиентском контексте</returns>
    Task<JsonElement[]> HistoryAsync(RiskActor actor, BusinessPeriod period, int offset, int limit);

    /// <summary>Создание согласованного снимка для выгрузки</summary>
    /// <param name="actor">Пользователь из проверенной сессии</param>
    /// <param name="period">Включительный диапазон дат UTC</param>
    /// <returns>Результат в клиентском контексте</returns>
    Task<BusinessExportDal> RequestExportAsync(RiskActor actor, BusinessPeriod period);

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

    /// <summary>Вычисляет предварительную оценку</summary>
    /// <param name="actor">Пользователь текущего клиента</param>
    /// <param name="id">Риск</param>
    /// <param name="request">Входы оценки</param>
    /// <returns>Результат операции</returns>
    Task<AssessmentPreview> PreviewAssessmentAsync(RiskActor actor, Guid id, AssessmentRequest request);

    /// <summary>Сохраняет бизнес-оценку и активирует риск</summary>
    /// <param name="actor">Пользователь текущего клиента</param>
    /// <param name="id">Риск</param>
    /// <param name="request">Входы расчёта и версии</param>
    /// <returns>Результат операции</returns>
    Task<AssessmentPreview> SaveAssessmentAsync(RiskActor actor, Guid id, SaveAssessmentRequest request);

    /// <summary>Читает историю оценок риска</summary>
    /// <param name="actor">Пользователь текущего клиента</param>
    /// <param name="id">Риск</param>
    /// <param name="offset">Смещение</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Результат операции</returns>
    Task<RiskAssessmentDal[]> ListAssessmentsAsync(RiskActor actor, Guid id, int offset, int limit);

    /// <summary>Читает опубликованную денежную шкалу</summary>
    /// <param name="actor">Пользователь текущего клиента</param>
    /// <param name="id">Версия шкалы или null для текущей</param>
    /// <param name="admin">Административный запрос</param>
    /// <returns>Результат операции</returns>
    Task<ImpactScale?> GetImpactScaleAsync(RiskActor actor, Guid? id, bool admin);

    /// <summary>Публикует денежную шкалу и выполняет пересчёт</summary>
    /// <param name="actor">Пользователь текущего клиента</param>
    /// <param name="request">Ожидаемая версия и нижние пороги</param>
    /// <returns>Результат операции</returns>
    Task<ImpactScale> SaveImpactScaleAsync(RiskActor actor, ImpactScaleRequest request);

    /// <summary>Читает пороги принятия</summary>
    /// <param name="actor">Пользователь текущего клиента</param>
    /// <returns>Результат операции</returns>
    Task<RiskAcceptanceSettingsDal> GetAcceptanceSettingsAsync(RiskActor actor);

    /// <summary>Сохраняет пороги принятия</summary>
    /// <param name="actor">Пользователь текущего клиента</param>
    /// <param name="request">Новые пороги и область применения</param>
    /// <returns>Результат операции</returns>
    Task<RiskAcceptanceSettingsDal> SaveAcceptanceSettingsAsync(RiskActor actor, AcceptanceSettingsRequest request);

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

    /// <summary>Передаёт риск новому основному процессу и открывает пересмотр</summary>
    /// <param name="actor">Текущий пользователь</param>
    /// <param name="id">Риск</param>
    /// <param name="request">Целевой процесс и версия</param>
    /// <returns>Обновлённый риск</returns>
    Task<RiskDal> TransferRiskAsync(RiskActor actor, Guid id, TransferRiskRequest request);

    /// <summary>Читает страницу истории пересмотра</summary>
    /// <param name="actor">Текущий пользователь</param>
    /// <param name="id">Риск</param>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>История с контекстом запуска</returns>
    Task<RiskReviewView[]> ListRiskReviewsAsync(RiskActor actor, Guid id, int offset, int limit);

    /// <summary>Читает доступные новые процессы для передачи</summary>
    /// <param name="actor">Текущий пользователь</param>
    /// <param name="id">Риск</param>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Страница допустимых процессов</returns>
    Task<RiskProcessOption[]> ListTransferProcessesAsync(RiskActor actor, Guid id, int offset, int limit);
}
