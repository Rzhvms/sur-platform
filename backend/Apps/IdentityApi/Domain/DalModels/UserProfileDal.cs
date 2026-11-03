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
}