namespace Application.Models;

/// <summary>Встроенный уровень матрицы</summary>
/// <param name="Id">Стабильный идентификатор</param>
/// <param name="Weight">Вес в матрице</param>
/// <param name="Name">Отображаемое название</param>
public sealed record AssessmentLevel(Guid Id, int Weight, string Name);
