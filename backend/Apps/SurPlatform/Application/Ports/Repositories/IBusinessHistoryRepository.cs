using Application.UseCases.Treatments.Dto.Request;
using Application.UseCases.Risks.Dto.Request;
using System.Text.Json;
using Application.Constants;
using Application.Models;
using Application.UseCases.Risks.Dto;
using Domain.DalModels;

namespace Application.Ports.Repositories;

/// <summary>Чтение административной истории и регистрация ее выгрузки</summary>
public interface IBusinessHistoryRepository
{
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
}
