using Application.Models;

namespace Application.Constants;

/// <summary>Неизменяемый встроенный метод оценки</summary>
public static class AssessmentMethod
{
    /// <summary>Стабильный идентификатор метода</summary>
    public static readonly Guid Id = new(MethodIdentifier);

    /// <summary>Идентификатор для хранения исторических оценок</summary>
    private const string MethodIdentifier = "be7d17a4-a396-4518-b240-b94822ce9d00";

    /// <summary>Код единственной настройки маршрутов</summary>
    public static readonly Guid AcceptanceSettingsId = Id;

    /// <summary>Число положительных весов</summary>
    public const int MaxWeight = 5;

    /// <summary>Верхняя граница оценки встроенного метода</summary>
    public const int MaxScore = MaxWeight * MaxWeight;

    /// <summary>Исходная граница владельца</summary>
    public const int DefaultOwnerMax = 10;

    /// <summary>Исходная граница директора</summary>
    public const int DefaultDirectorMax = 15;

    /// <summary>Исходная граница CEO</summary>
    public const int DefaultCeoMax = 19;

    /// <summary>Имя встроенного метода</summary>
    private const string Name = "Матрица рисков";

    /// <summary>Формула встроенного метода</summary>
    private const string Formula = "R = L × I";

    /// <summary>Стабильный формат идентификатора уровня</summary>
    private const string LevelIdFormat = "be7d17a4-a396-4518-b240-b94822ce9{0}{1:00}";

    /// <summary>Код оси вероятности в идентификаторе уровня</summary>
    private const int LikelihoodAxis = 1;

    /// <summary>Код оси тяжести в идентификаторе уровня</summary>
    private const int ImpactAxis = 2;

    /// <summary>Названия уровней вероятности в порядке весов</summary>
    private static readonly string[] LikelihoodNames =
        ["Невозможно", "Почти невозможно", "Маловероятно", "Возможно", "Вероятно", "Почти неизбежно"];

    /// <summary>Названия уровней тяжести в порядке весов</summary>
    private static readonly string[] ImpactNames =
        ["Нет влияния", "Незначительная", "Низкая", "Средняя", "Высокая", "Критическая"];

    /// <summary>Формирует каталог и матрицу положительных весов</summary>
    /// <returns>Справочник и 25 целочисленных оценок</returns>
    public static AssessmentMatrix Describe()
    {
        return new AssessmentMatrix(Id, Name, Formula,
            LikelihoodNames.Select((name, weight) => new AssessmentLevel(LevelId(LikelihoodAxis, weight), weight, name)).ToArray(),
            ImpactNames.Select((name, weight) => new AssessmentLevel(ImpactId(weight), weight, name)).ToArray(),
            Enumerable.Range(1, MaxWeight).Select(likelihood =>
                Enumerable.Range(1, MaxWeight).Select(impact => likelihood * impact).ToArray()).ToArray());
    }

    /// <summary>Находит вес известного уровня вероятности</summary>
    /// <param name="levelId">Идентификатор из каталога</param>
    /// <returns>Вес либо null для неизвестного уровня</returns>
    public static int? GetLikelihoodWeight(Guid levelId)
    {
        for (var weight = 0; weight <= MaxWeight; weight++)
        {
            if (levelId == LevelId(LikelihoodAxis, weight))
            {
                return weight;
            }
        }

        return null;
    }

    /// <summary>Возвращает стабильный идентификатор уровня тяжести</summary>
    /// <param name="weight">Вес уровня</param>
    /// <returns>Идентификатор уровня</returns>
    public static Guid ImpactId(int weight)
    {
        return LevelId(ImpactAxis, weight);
    }

    /// <summary>Формирует стабильный идентификатор уровня</summary>
    /// <param name="axis">Код шкалы</param>
    /// <param name="weight">Вес уровня</param>
    /// <returns>Идентификатор, не зависящий от клиента</returns>
    private static Guid LevelId(int axis, int weight)
    {
        return Guid.Parse(string.Format(System.Globalization.CultureInfo.InvariantCulture, LevelIdFormat, axis, weight));
    }
}
