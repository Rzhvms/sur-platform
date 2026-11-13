using Application.Constants;
namespace Application.Models;

/// <summary>
/// Административное представление учётной записи
/// </summary>
public sealed record IdentityUser
{
    /// <summary>
    /// Идентификатор пользователя
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName(IdentityJsonProperties.UserId)]
    public Guid Id { get; init; }

    /// <summary>
    /// Первое действующее назначение для совместимости ответа
    /// </summary>
    public Guid RoleId
    {
        get { return RoleIds.FirstOrDefault(); }
    }

    /// <summary>
    /// Имя, сохранённое прежней версией приложения
    /// </summary>
    public string? FirstName { get; init; }

    /// <summary>
    /// Фамилия, сохранённая прежней версией приложения
    /// </summary>
    public string? LastName { get; init; }

    /// <summary>
    /// Отчество, сохранённое прежней версией приложения
    /// </summary>
    public string? Patronymic { get; init; }

    /// <summary>
    /// Адрес входа
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Полное имя
    /// </summary>
    public string? FullName { get; init; }

    /// <summary>
    /// Состояние учётной записи
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Состояние доставки доступа
    /// </summary>
    public string? InvitationStatus { get; init; }

    /// <summary>
    /// Версия карточки
    /// </summary>
    public Guid Version { get; init; }

    /// <summary>
    /// Автор изменения
    /// </summary>
    public Guid? UpdatedBy { get; init; }

    /// <summary>
    /// Время создания
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Время изменения
    /// </summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Действующие назначения ролей
    /// </summary>
    public Guid[] RoleIds { get; init; } = [];

    /// <summary>
    /// Отображаемые названия назначенных ролей
    /// </summary>
    public string[] Roles { get; init; } = [];

    /// <summary>
    /// Назначения оргструктуры
    /// </summary>
    public string[] Responsibilities { get; init; } = [];
}
