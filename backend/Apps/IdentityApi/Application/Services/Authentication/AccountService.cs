using Application.Ports.Repositories;
using Application.Ports.Services.Encryption;
using Application.UseCases.Auth.Dto.Request;
using Core.Abstractions.Exceptions;
using Domain.DalModels;

namespace Application.Services.Authentication;

/// <summary>
/// Регистрация и проверка учётных данных пользователей
/// </summary>
public sealed class AccountService(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IPasswordEncryptionService encryptionService,
    TimeProvider clock)
{
    /// <summary>
    /// Создаёт пользователя либо возвращает отсутствие результата при занятой почте
    /// </summary>
    public async Task<Guid?> CreateAsync(CreateUserRequest request)
    {
        var email = request.Email.Trim();
        var normalizedEmail = email.ToLowerInvariant();

        if (await userRepository.ExistsByEmailAsync(normalizedEmail))
        {
            return null;
        }

        var role = await roleRepository.GetRoleByNameAsync(request.RoleName)
            ?? throw new EntityNotFoundException("Роль не найдена");
        var userId = Guid.NewGuid();
        var now = clock.GetUtcNow().UtcDateTime;
        var profile = new UserProfileDal
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Email = email,
            NormalizedEmail = normalizedEmail,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Patronymic = request.Patronymic,
            CreatedAt = now,
            UpdatedAt = now
        };
        var credential = new UserCredentialDal
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Password = HashPassword(request.Password),
            PasswordChangedAt = now
        };

        await userRepository.CreateUserAsync(profile, credential, role.Id);

        return userId;
    }

    /// <summary>
    /// Возвращает идентификатор пользователя после успешной проверки пароля и состояния
    /// </summary>
    public async Task<Guid?> AuthenticateAsync(string email, string password)
    {
        var user = await userRepository.GetLoginDataAsync(email.Trim().ToLowerInvariant());
        var now = clock.GetUtcNow().UtcDateTime;

        if (user is null || !user.IsActive || user.LockoutUntil > now ||
            !encryptionService.VerifyPassword(password, user.Password))
        {
            return null;
        }

        return user.UserId;
    }

    /// <summary>
    /// Изменяет пароль пользователя с отзывом его сессий в репозитории
    /// </summary>
    public async Task ChangePasswordAsync(Guid userId, string password)
    {
        if (!await userRepository.ExistsAsync(userId))
        {
            throw new EntityNotFoundException("Пользователь не найден");
        }

        var passwordHash = HashPassword(password);
        await userRepository.ChangeUserPasswordAsync(userId, passwordHash, clock.GetUtcNow().UtcDateTime);
    }

    /// <summary>
    /// Создаёт самодостаточный хеш пароля с новой солью
    /// </summary>
    private string HashPassword(string password)
    {
        return encryptionService.HashPassword(password, encryptionService.GenerateSalt());
    }
}
