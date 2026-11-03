using Application.Helpers.Encryption;
using Application.Ports.Repositories;
using Application.UseCases.Auth.Dto.Request;
using Core.Abstractions.Exceptions;
using Domain.DalModels;

namespace Application.Services.Authentication;

/// <summary>
/// Регистрация и проверка учетных данных пользователей
/// </summary>
public sealed class AccountService(IUserRepository userRepository, IRoleRepository roleRepository)
{
    /// <summary>
    /// Создание пользователя либо возврат отсутствия результата при занятой почте
    /// </summary>
    public async Task<Guid?> CreateAsync(CreateUserRequest request)
    {
        var email = request.Email.Trim();
        var normalizedEmail = email.ToLowerInvariant();

        var isEmailExists = await userRepository.ExistsByEmailAsync(normalizedEmail);
        if (isEmailExists)
        {
            return null;
        }

        var role = await roleRepository.GetRoleByNameAsync(request.RoleName);
        
        var userId = Guid.NewGuid();
        var currentDateTime = DateTime.UtcNow;
        
        var profile = new UserProfileDal
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Email = email,
            NormalizedEmail = normalizedEmail,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Patronymic = request.Patronymic,
            CreatedAt = currentDateTime,
            UpdatedAt = currentDateTime
        };
        
        var credential = new UserCredentialDal
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Password = HashPassword(request.Password),
            PasswordChangedAt = currentDateTime
        };

        await userRepository.CreateUserAsync(profile, credential, role!.Id);

        return userId;
    }

    /// <summary>
    /// Возвращает идентификатор пользователя после успешной проверки пароля и состояния
    /// </summary>
    public async Task<Guid?> AuthenticateAsync(string email, string password)
    {
        var user = await userRepository.GetLoginDataAsync(email.Trim().ToLowerInvariant());
        var currentDateTime = DateTime.UtcNow;

        if (user is null || !user.IsActive || user.LockoutUntil > currentDateTime ||
            !PasswordEncryptionHelper.VerifyPassword(password, user.Password))
        {
            return null;
        }

        return user.UserId;
    }

    /// <summary>
    /// Изменение пароля пользователя с отзывом его сессий в репозитории
    /// </summary>
    public async Task ChangePasswordAsync(Guid userId, string password)
    {
        var isUserExist = await userRepository.IsExistAsync(userId);
        if (!isUserExist)
        {
            throw new EntityNotFoundException($"Пользователь {userId} не найден");
        }

        var passwordHash = HashPassword(password);
        await userRepository.ChangeUserPasswordAsync(userId, passwordHash, DateTime.UtcNow);
    }

    /// <summary>
    /// Создание хеша пароля
    /// </summary>
    private string HashPassword(string password)
    {
        var hashSalt = PasswordEncryptionHelper.GenerateSalt();
        return PasswordEncryptionHelper.HashPassword(password, hashSalt);
    }
}