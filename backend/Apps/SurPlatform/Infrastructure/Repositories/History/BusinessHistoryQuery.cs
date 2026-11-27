using Infrastructure.Repositories.History.Helpers;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.UseCases.Risks.Dto;
using Core.Database.DapperExtensions.EntityMapper;
using Domain.DalModels;

namespace Infrastructure.Repositories.History;

/// <summary>Чтение административной истории</summary>
internal static class BusinessHistoryQuery
{
    /// <summary>Единая проекция обработанных событий и ещё ожидающих поручений</summary>
    public static readonly string JournalSource = $"""
        SELECT {EntityMapper.ColName<BusinessChangeDal>(x => x.EventId)}, {EntityMapper.ColName<BusinessChangeDal>(x => x.OccurredAt)}, {EntityMapper.ColName<BusinessChangeDal>(x => x.Payload)} FROM {EntityMapper.TbName<BusinessChangeDal>()}
        UNION ALL
        SELECT j.{EntityMapper.ColName<BusinessChangeJobDal>(x => x.EventId)}, j.{EntityMapper.ColName<BusinessChangeJobDal>(x => x.OccurredAt)}, j.{EntityMapper.ColName<BusinessChangeJobDal>(x => x.Payload)} FROM {EntityMapper.TbName<BusinessChangeJobDal>()} j
        WHERE NOT EXISTS (SELECT FROM {EntityMapper.TbName<BusinessChangeDal>()} e WHERE e.{EntityMapper.ColName<BusinessChangeDal>(x => x.EventId)} = j.{EntityMapper.ColName<BusinessChangeJobDal>(x => x.EventId)})
        """;

    /// <summary>Преобразует включительные даты в полуоткрытый UTC-интервал</summary>
    /// <param name="period">Даты запроса</param>
    /// <returns>Первая полночь и исключённая полночь следующего дня</returns>
    public static (DateTime From, DateTime Until) Boundaries(BusinessPeriod period)
    {
        if (period.From == default || period.To < period.From || period.To == DateOnly.MaxValue)
        {
            throw new IdentityCommandException(400, RiskRules.Invalid);
        }

        return (period.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            period.To.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
    }
}
