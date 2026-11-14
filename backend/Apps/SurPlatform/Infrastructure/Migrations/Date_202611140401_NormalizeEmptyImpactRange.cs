using Core.Database.DapperExtensions.EntityMapper;
using Domain.DalModels;
using FluentMigrator;

namespace Infrastructure.Migrations;

/// <summary>Нормализует верхнюю границу опубликованных пустых денежных диапазонов</summary>
[Migration(202611140401)]
public sealed class Date_202611140401_NormalizeEmptyImpactRange : Migration
{
    /// <summary>Прежнее значение верхней границы пустого диапазона</summary>
    private const string LegacyEmptyMaximum = "-0.01";

    /// <inheritdoc/>
    public override void Up()
    {
        if (Schema.Table(EntityMapper.TbName<ImpactThresholdDal>(false)).Exists())
        {
            var query = $"""
                UPDATE {EntityMapper.TbName<ImpactThresholdDal>()}
                SET {EntityMapper.ColName<ImpactThresholdDal>(x => x.MaxLossAmount)} = NULL
                WHERE {EntityMapper.ColName<ImpactThresholdDal>(x => x.Weight)} = 0
                  AND {EntityMapper.ColName<ImpactThresholdDal>(x => x.MinLossAmount)} = 0
                  AND {EntityMapper.ColName<ImpactThresholdDal>(x => x.MaxLossAmount)} = {LegacyEmptyMaximum}
                """;
            Execute.Sql(query);
        }
    }

    /// <inheritdoc/>
    public override void Down()
    {
        if (Schema.Table(EntityMapper.TbName<ImpactThresholdDal>(false)).Exists())
        {
            var query = $"""
                UPDATE {EntityMapper.TbName<ImpactThresholdDal>()}
                SET {EntityMapper.ColName<ImpactThresholdDal>(x => x.MaxLossAmount)} = {LegacyEmptyMaximum}
                WHERE {EntityMapper.ColName<ImpactThresholdDal>(x => x.Weight)} = 0
                  AND {EntityMapper.ColName<ImpactThresholdDal>(x => x.MinLossAmount)} = 0
                  AND {EntityMapper.ColName<ImpactThresholdDal>(x => x.MaxLossAmount)} IS NULL
                """;
            Execute.Sql(query);
        }
    }
}
