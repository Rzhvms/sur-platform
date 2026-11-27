using System.Globalization;
using Application.Constants;
using Application.Exceptions;
using Domain.DalModels;
using Domain.DalModels.Treatments;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Services.Jira.Helpers;

/// <summary>
/// Проверка адресов и подготовка полей запросов Jira без выполнения HTTP-обмена
/// </summary>
internal static class JiraRequestHelper
{
    /// <summary>Разрешённые базовые URL из конфигурации развёртывания</summary>
    private const string AllowedUrls = "Jira:AllowedBaseUrls";

    /// <summary>Поиск исполнителя Jira по переданному рабочему email</summary>
    internal const string UserSearchPath = "user/search?username=";

    /// <summary>Поле описания задачи, которое заполняется только при наличии описания меры</summary>
    internal const string DescriptionField = "description";

    /// <summary>Разрешает ровно один настроенный HTTPS URL с контекстным путём</summary>
    /// <param name="configuration">Конфигурация оператора</param>
    /// <param name="value">Запрошенный базовый URL</param>
    /// <returns>Проверенный базовый адрес</returns>
    /// <exception cref="IdentityCommandException">Адрес не разрешён</exception>
    internal static Uri Resolve(IConfiguration configuration, string value)
    {
        if (!Uri.TryCreate(value.TrimEnd('/') + "/", UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0
            || !configuration.GetSection(AllowedUrls).GetChildren().Any(entry =>
                Uri.TryCreate(entry.Value?.TrimEnd('/') + "/", UriKind.Absolute, out var allowed)
                && uri.Equals(allowed) && !uri.IsLoopback))
        {
            throw new IdentityCommandException(400, JiraRules.Invalid);
        }
        return uri;
    }

    /// <summary>Проверяет идентификатор, используемый как сегмент URL Jira</summary>
    /// <param name="value">Ключ проекта, задачи или идентификатор</param>
    /// <returns>Признак безопасного сегмента</returns>
    internal static bool IsResourceId(string value)
    {
        return value.Length is > 0 and <= 128 && value.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_');
    }

    /// <summary>Выбирает поля создания, соответствующие календарной дате меры</summary>
    /// <param name="settings">Снимок подключения</param>
    /// <param name="measure">Мера для Jira</param>
    /// <param name="assignee">Логин исполнителя Jira</param>
    /// <returns>Поля создания Jira</returns>
    internal static Dictionary<string, object?> CreateFields(JiraSettingsDal settings, TreatmentDal measure, string assignee)
    {
        var fields = new Dictionary<string, object?>
        {
            ["project"] = new { key = settings.Project },
            ["issuetype"] = new { id = settings.IssueType },
            ["summary"] = measure.Title,
            ["assignee"] = new { name = assignee },
            ["duedate"] = DateOnly.FromDateTime(measure.DueDate).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };
        if (!string.IsNullOrWhiteSpace(measure.Description))
        {
            fields[DescriptionField] = measure.Description;
        }
        return fields;
    }
}
