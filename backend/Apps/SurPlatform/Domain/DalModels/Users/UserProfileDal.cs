using System.ComponentModel.DataAnnotations;
using Domain.DalModels.Base;

namespace Domain.DalModels.Users;

/// <summary>
/// Профиль учетной записи с состоянием доступа и выдачи временного пароля
/// </summary>
public sealed record UserProfileDal : BaseUserModel
{
    /// <summary>
    /// Имя пользователя
    /// </summary>
    [MaxLength(50)]
    public string? FirstName { get; init; }

    /// <summary>
    /// Фамилия пользователя
    /// </summary>
    [MaxLength(50)]
    public string? LastName { get; init; }

    /// <summary>
    /// Отчество пользователя при его наличии
    /// </summary>
    [MaxLength(50)]
    public string? Patronymic { get; init; }

    /// <summary>
    /// Номер телефона пользователя
    /// </summary>
    [Phone]
    [MaxLength(20)]
    public string? Phone { get; init; }

    /// <summary>
    /// Адрес электронной почты пользователя
    /// </summary>
    [EmailAddress]
    [MaxLength(50)]
    public string? Email { get; init; }

    /// <summary>
    /// Нормализованный email для поиска и проверки уникальности
    /// </summary>
    [EmailAddress]
    [MaxLength(50)]
    public string? NormalizedEmail { get; init; }

    /// <summary>
    /// Признак учетной записи, которой разрешен вход
    /// </summary>
    public bool IsActive { get; init; } = true;

    /// <summary>
    /// Признак подтвержденного адреса электронной почты
    /// </summary>
    public bool IsEmailConfirmed { get; init; }

    /// <summary>
    /// Признак подтвержденного номера телефона
    /// </summary>
    public bool IsPhoneConfirmed { get; init; }

    /// <summary>
    /// Время окончания временной блокировки входа в UTC
    /// </summary>
    public DateTime? LockoutUntil { get; init; }

    /// <summary>
    /// Время создания учетной записи в UTC
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Время последнего изменения учетной записи в UTC
    /// </summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Полное имя пользователя для отображения в карточке
    /// </summary>
    public string? FullName { get; init; }

    /// <summary>
    /// Версия доступа и учетных данных для проверки актуальности JWT
    /// </summary>
    public long AccessVersion { get; init; }

    /// <summary>
    /// Требование сменить временный пароль до получения обычного доступа
    /// </summary>
    public bool MustChangePassword { get; init; }

    /// <summary>
    /// Время окончания действия временного пароля в UTC
    /// </summary>
    public DateTime? TemporaryPasswordExpiresAt { get; init; }

    /// <summary>
    /// Сохраненное состояние выдачи временного доступа
    /// </summary>
    public string? InvitationStatus { get; init; }

    /// <summary>
    /// Идентификатор версии выдачи временного доступа
    /// </summary>
    public Guid? InvitationVersion { get; init; }

    /// <summary>
    /// Время начала выдачи временного доступа в UTC
    /// </summary>
    public DateTime? InvitationStartedAt { get; init; }

    /// <summary>
    /// Идентификатор пользователя или системного субъекта, последним изменившего запись
    /// </summary>
    public Guid? UpdatedBy { get; init; }

    /// <summary>
    /// Признак начального администратора для служебной повторной выдачи доступа
    /// </summary>
    public bool IsFirstAdministrator { get; init; }
}