using Application.Services.Authentication.Interfaces;
using Application.Constants;
using Application.Exceptions;
using Application.Helpers.Encryption;
using Application.Models;
using Application.Ports.Repositories;
using Application.UseCases.Users.Dto.Request;

namespace Application.Services.Authentication;

/// <summary>Выдаёт временный доступ с паролем администратора без внешнего транспорта</summary>
/// <param name="repository">Хранилище выдачи текущего клиента</param>
internal sealed class IdentityAccessService(IIdentityAdministrationRepository repository) : IIdentityAccessService
{
    /// <summary>Причина отказа при отсутствии сохранённой учётной записи</summary>
    private const string IssuedAccountMissing = "Выданная учётная запись отсутствует";

    /// <inheritdoc/>
    public async Task<IdentityUser> CreateAsync(Guid actor, Guid sessionId, CreateIdentityUserRequest request)
    {
        var passwordHash = Hash(request.TemporaryPassword);
        var userId = await repository.CreateAsync(actor, sessionId, request, passwordHash);
        return await ReadAsync(userId);
    }

    /// <inheritdoc/>
    public async Task<IdentityUser> ReissueAsync(Guid actor, Guid sessionId, Guid userId,
        ActivateIdentityUserRequest? activation, Guid version, string temporaryPassword)
    {
        var passwordHash = Hash(temporaryPassword);
        await repository.IssueAsync(actor, sessionId, userId, activation, version, passwordHash);
        return await ReadAsync(userId);
    }

    /// <inheritdoc/>
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
