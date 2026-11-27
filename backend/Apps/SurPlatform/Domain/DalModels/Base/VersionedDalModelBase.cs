namespace Domain.DalModels.Base;

/// <summary>
/// Основа записи с контролем конкурентных изменений
/// </summary>
public abstract record VersionedDalModelBase : DalModelBase
{
    /// <summary>
    /// Версия записи, с которой сравнивается ожидаемая версия команды
    /// </summary>
    public Guid Version { get; init; }
}