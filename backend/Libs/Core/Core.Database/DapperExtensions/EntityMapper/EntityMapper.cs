using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Core.Database.DapperExtensions.EntityMapper;

/// <summary>
/// Вспомогательный компонент для получения имен таблиц и колонок на основе моделей домена
/// Используется при генерации SQL-запросов, миграций и других сценариях
/// </summary>
public static class EntityMapper
{
    #region Description
    // Варианты использования:
    // 1. Название таблицы с кавычками, "User":
    // var table = EntityMapper.TbName<User>();
    // 2. Название таблицы без кавычек, User:
    // var quotedTable = EntityMapper.TbName<User>(withQuotes: false);
    // 3. Название колонки с кавычками, "Id":
    // var column = EntityMapper.ColName&lt;User&gt;(x => x.Id);
    // 4. Название колонки без кавычек, Id:
    // var quotedColumn = EntityMapper.ColName&lt;User&gt;(x => x.Id, withQuotes: false);
    #endregion
    
    private static readonly ConcurrentDictionary<Type, string> TableNameCache = new();
    private static readonly ConcurrentDictionary<MemberInfo, string> ColumnNameCache = new();

    /// <summary>
    /// Префикс именованного параметра SQL
    /// </summary>
    public static string ParameterPrefix => NamedParameterPrefix;

    /// <summary>
    /// Синтаксис именованного параметра для провайдера базы
    /// </summary>
    private const string NamedParameterPrefix = "@";

    /// <summary>
    /// Формирует ссылку на именованный параметр SQL
    /// </summary>
    /// <param name="name">Имя параметра без префикса</param>
    /// <returns>Ссылка для включения в запрос</returns>
    public static string ParameterName(string name)
    {
        return ParameterPrefix + name;
    }

    /// <summary>
    /// Получение имени таблицы для сущности типа <typeparamref name="TEntity"/>
    /// </summary>
    /// <param name="withQuotes">Нужно ли оборачивать имя в двойные кавычки</param>
    public static string TbName<TEntity>(bool withQuotes = true) => TbName(typeof(TEntity), withQuotes);

    /// <summary>
    /// Получение имени таблицы для указанного CLR-типа
    /// </summary>
    /// <param name="entityType">Тип сущности</param>
    /// <param name="withQuotes">Нужно ли оборачивать имя в двойные кавычки</param>
    private static string TbName(Type entityType, bool withQuotes = true)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        var name = TableNameCache.GetOrAdd(entityType, static t => t.Name);
        return withQuotes ? Identifier(name) : name;
    }

    /// <summary>
    /// Получение имени колонки, соответствующей свойству или полю сущности
    /// </summary>
    /// <param name="expression">Выражение, указывающее на свойство или поле</param>
    /// <param name="withQuotes">Нужно ли оборачивать имя в двойные кавычки</param>
    public static string ColName<TEntity>(Expression<Func<TEntity, object?>> expression, bool withQuotes = true)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var member = ExtractMember(expression);
        var name = ColumnNameCache.GetOrAdd(member, static m => m.Name);

        return withQuotes ? Identifier(name) : name;
    }

    /// <summary>
    /// Извлечение <see cref="MemberInfo"/> из лямбда-выражения,
    /// поддерживая вложенные свойства и приведения типов
    /// </summary>
    private static MemberInfo ExtractMember<TEntity>(Expression<Func<TEntity, object?>> expression)
    {
        var body = expression.Body;

        // Убираем boxing / Convert для value-type (x => (object)x.Id)
        while (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
        {
            body = unary.Operand;
        }

        var memberExpression = ExtractDeepestMemberExpression(body);
        return memberExpression.Member;
    }

    /// <summary>
    /// Сообщение для выражения, которое не указывает на столбец модели
    /// </summary>
    private const string InvalidColumnExpressionFormat = "Expression '{0}' must be a property or field access";

    /// <summary>
    /// Извлечение самого глубокого <see cref="MemberExpression"/> из выражения
    /// </summary>
    private static MemberExpression ExtractDeepestMemberExpression(Expression expression)
    {
        MemberExpression? result = null;
        var current = expression;

        while (current is MemberExpression member)
        {
            result = member;
            current = member.Expression!;
        }

        if (result is null)
        {
            throw new ArgumentException(string.Format(System.Globalization.CultureInfo.InvariantCulture, InvalidColumnExpressionFormat, expression));
        }

        return result;
    }

    /// <summary>
    /// Обернуть параметр в двойные кавычки
    /// </summary>
    /// <param name="parameter">Имя объекта базы данных</param>
    /// <returns>Экранированный идентификатор SQL</returns>
    public static string Identifier(string parameter)
    {
        return DoubleQuote + parameter.Replace(DoubleQuote, EscapedDoubleQuote, StringComparison.Ordinal) + DoubleQuote;
    }
    /// <summary>
    /// Разделитель имени объекта SQL
    /// </summary>
    private const string DoubleQuote = "\"";

    /// <summary>
    /// Кавычка внутри идентификатора SQL
    /// </summary>
    private const string EscapedDoubleQuote = "\"\"";
}