using Domain.DalModels;

namespace Application.Models;

/// <summary>Паспорт с текущей оценкой и отображаемыми связями</summary>
/// <param name="Risk">Поле Risk представления</param>
/// <param name="Processes">Поле Processes представления</param>
/// <param name="CurrentAssessment">Поле CurrentAssessment представления</param>
/// <param name="HazardName">Поле HazardName представления</param>
/// <param name="CategoryName">Поле CategoryName представления</param>
/// <param name="AssetName">Поле AssetName представления</param>
/// <param name="AuthorName">Поле AuthorName представления</param>
public sealed record RiskRow(RiskDal Risk, RiskProcessView[] Processes, RiskAssessmentDal? CurrentAssessment, string HazardName, string CategoryName, string? AssetName, string? AuthorName);
