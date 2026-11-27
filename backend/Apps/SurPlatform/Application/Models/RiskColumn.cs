namespace Application.Models;

/// <summary>Страница колонки канбан-доски и итог после фильтрации</summary>
/// <param name="Status">Статус колонки</param>
/// <param name="Count">Количество карточек в текущей странице</param>
/// <param name="Items">Карточки текущей страницы</param>
public sealed record RiskColumn(string Status, long Count, RiskRow[] Items)
{
    /// <summary>Количество подходящих рисков в колонке до пагинации</summary>
    public long TotalCount { get; init; }
}
