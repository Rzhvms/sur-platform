using Application.Constants;
using Application.Exceptions;
using Application.Helpers.Encryption;
using Application.Models;
using Application.Ports.Repositories;
using Application.UseCases.Users.Dto.Request;

namespace Application.Services.Authentication;

/// <summary>Выдаёт временный доступ с паролем администратора без внешнего транспорта</summary>
/// <param name="repository">Хранилище выдачи текущего клиента</param>
public sealed class IdentityAccessService(IIdentityAdministrationRepository repository)
{
    /// <summary>Причина отказа при отсутствии сохранённой учётной записи</summary>
    private const string IssuedAccountMissing = "Выданная учётная запись отсутствует";

    /// <summary>Создаёт пользователя, роли и начальные оргназначения с временным доступом</summary>
    /// <param name="actor">Администратор</param>
    /// <param name="sessionId">Сессия администратора</param>
    /// <param name="request">Данные и временный пароль</param>
    /// <returns>Созданная карточка без исходного пароля</returns>
    public async Task<IdentityUser> CreateAsync(Guid actor, Guid sessionId, CreateIdentityUserRequest request)
    {
        var passwordHash = Hash(request.TemporaryPassword);
        var userId = await repository.CreateAsync(actor, sessionId, request, passwordHash);
        return await ReadAsync(userId);
    }

    /// <summary>Заменяет временный пароль и при необходимости активирует пользователя</summary>
    /// <param name="actor">Администратор</param>
    /// <param name="sessionId">Сессия администратора</param>
    /// <param name="userId">Получатель доступа</param>
    /// <param name="activation">Параметры активации или отсутствие активации</param>
    /// <param name="version">Ожидаемая версия карточки</param>
    /// <param name="temporaryPassword">Пароль для самостоятельной передачи пользователю</param>
    /// <returns>Карточка без исходного пароля</returns>
    public async Task<IdentityUser> ReissueAsync(Guid actor, Guid sessionId, Guid userId,
        ActivateIdentityUserRequest? activation, Guid version, string temporaryPassword)
    {
        var passwordHash = Hash(temporaryPassword);
        await repository.IssueAsync(actor, sessionId, userId, activation, version, passwordHash);
        return await ReadAsync(userId);
    }

    /// <summary>Настраивает первоначальный локальный административный доступ</summary>
    /// <param name="email">Адрес администратора</param>
    /// <param name="fullName">ФИО администратора</param>
    /// <param name="password">Пароль из доверенной конфигурации</param>
    /// <param name="reissue">Явная повторная выдача оператором</param>
    /// <returns>Результат настройки или отсутствие изменения</returns>
    public async Task<string?> BootstrapAsync(string email, string fullName, string password, bool reissue)
    {
        var configured = await repository.BootstrapAsync(email, fullName, Hash(password), reissue);
        return configured ? IdentityAccounts.DevelopmentAccessConfigured : null;
    }

    /// <summary>Читает результат завершённой транзакции выдачи</summary>
    /// <param name="userId">Получатель доступа</param>
    /// <returns>Карточка пользователя</returns>
    /// <exception cref="InvalidOperationException">Созданная запись отсутствует</exception>
    private async Task<IdentityUser> ReadAsync(Guid userId)
    {
        return await repository.FindAsync(userId) ?? throw new InvalidOperationException(IssuedAccountMissing);
    }

    /// <summary>Проверяет пароль перед хешированием с независимой солью</summary>
    /// <param name="password">Неизменённое значение, предоставленное администратором</param>
    /// <returns>Защищённое представление для сохранения</returns>
    /// <exception cref="IdentityCommandException">Пароль отсутствует или нарушает ограничения</exception>
    private static string Hash(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < IdentityAccounts.MinPasswordLength ||
            password.Length > IdentityAccounts.MaxPasswordLength)
        {
            throw new IdentityCommandException(400, IdentityErrorCodes.InvalidTemporaryPassword);
        }

        return PasswordEncryptionHelper.HashPassword(password, PasswordEncryptionHelper.GenerateSalt());
    }
}
