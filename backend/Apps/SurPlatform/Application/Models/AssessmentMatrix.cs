namespace Application.Models;

/// <summary>Справочник встроенного метода с матрицей положительных весов</summary>
/// <param name="MethodId">Встроенный метод</param>
/// <param name="Name">Отображаемое название</param>
/// <param name="Formula">Формула расчёта</param>
/// <param name="LikelihoodLevels">Уровни вероятности</param>
/// <param name="ImpactLevels">Уровни тяжести</param>
/// <param name="Scores">Строки вероятности и столбцы тяжести, веса 1–5</param>
public sealed record AssessmentMatrix(Guid MethodId, string Name, string Formula, AssessmentLevel[] LikelihoodLevels, AssessmentLevel[] ImpactLevels, int[][] Scores);
