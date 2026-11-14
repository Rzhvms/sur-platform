using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Infrastructure.Observability;

/// <summary>Измерения фоновых операций без идентификаторов клиентов и содержимого запросов</summary>
internal sealed class OperationsMetrics
{
    /// <summary>Имя источника инфраструктурных метрик</summary>
    private const string MeterName = "SurPlatform.Operations";

    /// <summary>Единица измерения времени</summary>
    private const string Seconds = "s";

    /// <summary>Технический вид операции из закрытого набора</summary>
    private const string OperationTag = "operation";

    /// <summary>Подтверждённый итог из закрытого набора</summary>
    private const string OutcomeTag = "outcome";

    /// <summary>Длительность фоновой попытки</summary>
    private readonly Histogram<double> _duration;

    /// <summary>Возраст поручения к началу обработки</summary>
    private readonly Histogram<double> _age;

    /// <summary>Количество фоновых исходов и конфликтов</summary>
    private readonly Counter<long> _outcomes;

    /// <summary>Создаёт измерения с жизненным циклом веб-хоста</summary>
    /// <param name="factory">Фабрика измерителей DI</param>
    public OperationsMetrics(IMeterFactory factory)
    {
        var meter = factory.Create(MeterName);
        _duration = meter.CreateHistogram<double>("sur.operation.duration", Seconds);
        _age = meter.CreateHistogram<double>("sur.operation.age", Seconds);
        _outcomes = meter.CreateCounter<long>("sur.operation.outcomes");
    }

    /// <summary>Измеряет завершившуюся попытку</summary>
    /// <param name="operation">Предопределённый вид работы</param>
    /// <param name="started">Отметка монотонных часов перед началом</param>
    public void Duration(string operation, long started)
    {
        _duration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds, new KeyValuePair<string, object?>(OperationTag, operation));
    }

    /// <summary>Измеряет время от постановки или события до текущего момента</summary>
    /// <param name="operation">Предопределённый вид работы</param>
    /// <param name="queuedAt">Момент появления работы в UTC</param>
    public void Age(string operation, DateTime queuedAt)
    {
        _age.Record(Math.Max(0, (DateTime.UtcNow - queuedAt).TotalSeconds), new KeyValuePair<string, object?>(OperationTag, operation));
    }

    /// <summary>Учитывает результат без раскрытия текста ошибки</summary>
    /// <param name="operation">Предопределённый вид работы</param>
    /// <param name="outcome">Предопределённое состояние</param>
    public void Outcome(string operation, string outcome)
    {
        _outcomes.Add(1, new KeyValuePair<string, object?>(OperationTag, operation),
            new KeyValuePair<string, object?>(OutcomeTag, outcome));
    }
}
