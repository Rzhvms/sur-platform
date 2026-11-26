using Core.Database.DapperExtensions.EntityMapper;
using Domain.DalModels;
using FluentMigrator;

namespace Infrastructure.Migrations;

/// <summary>Добавляет маркер однократного заполнения без самих демоданных</summary>
[Migration(202611260100)]
public sealed class Date_202611260100_AddDemoDataSeed : Migration
{
    /// <inheritdoc/>
    public override void Up()
    {
        var table = EntityMapper.TbName<DemoDataSeedDal>(false);
        if (!Schema.Table(table).Exists())
        {
            Create.Table(table)
                .WithColumn(EntityMapper.ColName<DemoDataSeedDal>(x => x.Id, false)).AsInt32().PrimaryKey()
                .WithColumn(EntityMapper.ColName<DemoDataSeedDal>(x => x.AppliedAt, false)).AsDateTimeOffset().NotNullable();
        }
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Откат требует восстановления полной клиентской базы</exception>
    public override void Down()
    {
        // Удаление маркера позволило бы повторно запустить набор поверх изменённых данных.
        throw new InvalidOperationException(RestoreRequired);
    }

    /// <summary>Откат маркера и связанных бизнес-данных выполняется только совместно</summary>
    private const string RestoreRequired = "Откат демонстрационного набора выполняется восстановлением клиентской базы";
}
