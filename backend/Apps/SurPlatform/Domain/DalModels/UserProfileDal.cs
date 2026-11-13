using System.ComponentModel.DataAnnotations;

namespace Domain.DalModels;

/// <summary>
/// Модель профиля пользователя
/// </summary>
public sealed record UserProfileDal
{
    /// <summary>
    /// Идентификатор записи
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Идентификатор пользователя
    /// </summary>
    public Guid UserId { get; init; }

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
    /// Отчество пользователя
    /// </summary>
    [MaxLength(50)]
    public string? Patronymic { get; init; }

    /// <summary>
    /// Номер телефона
    /// </summary>
    [Phone]
    [MaxLength(20)]
    public string? Phone { get; init; }

    /// <summary>
    /// Электронная почта
    /// </summary>
    [EmailAddress]
    [MaxLength(50)]
    public string? Email { get; init; }

    /// <summary>
    /// Нормализованный адрес электронной почты
    /// </summary>
    [EmailAddress]
    [MaxLength(50)]
    public string? NormalizedEmail { get; init; }

    /// <summary>
    /// Признак действующего пользователя
    /// </summary>
    public bool IsActive { get; init; } = true;

    /// <summary>
    /// Признак подтверждения электронной почты
    /// </summary>
    public bool IsEmailConfirmed { get; init; }

    /// <summary>
    /// Признак подтверждения номера телефона
    /// </summary>
    public bool IsPhoneConfirmed { get; init; }

    /// <summary>
    /// Время окончания временной блокировки входа
    /// </summary>
    public DateTime? LockoutUntil { get; init; }

    /// <summary>
    /// Время создания в UTC
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Время изменения в UTC
    /// </summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Полное имя для карточки учётной записи
    /// </summary>
    public string? FullName { get; init; }

    /// <summary>
    /// Версия для контроля конкурентных изменений
    /// </summary>
    public Guid Version { get; init; }

    /// <summary>
    /// Версия прав и учётных данных для JWT
    /// </summary>
    public long AccessVersion { get; init; }

    /// <summary>
    /// Ограничение доступа до смены временного пароля
    /// </summary>
    public bool MustChangePassword { get; init; }

    /// <summary>
    /// Момент окончания временного доступа
    /// </summary>
    public DateTime? TemporaryPasswordExpiresAt { get; init; }

    /// <summary>
    /// Состояние отправки письма
    /// </summary>
    public string? InvitationStatus { get; init; }

    /// <summary>
    /// Версия выдачи для защиты от запоздавшего результата
    /// </summary>
    public Guid? InvitationVersion { get; init; }

    /// <summary>
    /// Время начала текущей выдачи
    /// </summary>
    public DateTime? InvitationStartedAt { get; init; }

    /// <summary>
    /// Пользователь или системный субъект, изменивший запись
    /// </summary>
    public Guid? UpdatedBy { get; init; }

    /// <summary>
    /// Признак записи для служебной повторной выдачи
    /// </summary>
    public bool IsFirstAdministrator { get; init; }
}
