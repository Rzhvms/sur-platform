using Domain.DalModels;

namespace Application.Models;

/// <summary>Статусная колонка и число строк после фильтрации</summary>
/// <param name="Status">Поле Status представления</param>
/// <param name="Count">Поле Count представления</param>
/// <param name="Items">Поле Items представления</param>
public sealed record RiskColumn(string Status, long Count, RiskRow[] Items);
