using Application.UseCases.Treatments.Dto.Request;
using Application.UseCases.Risks.Dto.Request;
using System.Text.Json;
using Application.Constants;
using Application.Models;
using Application.UseCases.Risks.Dto;
using Domain.DalModels;

namespace Application.Ports.Repositories;

/// <summary>Справочники рисков и доступные пользователю варианты выбора</summary>
public interface IRiskReferenceRepository
{
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
}
