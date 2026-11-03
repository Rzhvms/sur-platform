using Application.Constants;
using Application.Ports.Repositories;
using Application.Ports.Services.Encryption;
using Application.UseCases.Users.Dto.Request;
using Application.UseCases.Users.Dto.Response;
using Application.UseCases.Users.Interfaces;
using Core.Abstractions.Exceptions;
using Domain.DalModels;

namespace Application.UseCases.Users;

/// <inheritdoc />
internal sealed class UserUseCaseManager(
    IUserRepository users, IPasswordEncryptionService passwords,
    TimeProvider clock) : IUserUseCaseManager
{
    /// <inheritdoc/>
    public async Task<GetUserListResponse> GetUserListAsync() => new()
    {
        UserList = (await users.GetAllUsersAsync()).Select(Map).ToList()
    };

    /// <inheritdoc/>
    public async Task<GetUserResponse> GetUserByIdAsync(Guid userId) =>
        Map(await users.GetUserByUserIdAsync(userId) ?? throw new EntityNotFoundException("Пользователь не найден"));

    /// <inheritdoc/>
    public async Task DeleteUserByIdAsync(Guid userId)
    {

        await users.DeleteUserByIdAsync(userId);

    }

    /// <inheritdoc/>
    public async Task<ChangeUserPasswordResponse> ChangePasswordAsync(Guid id, ChangeUserPasswordRequest request)
    {

        var credential = await users.GetCredentialAsync(id);
        if (credential is null || !passwords.VerifyPassword(request.OldPassword, credential.Password))
            return new ChangeUserPasswordResponse { IsSuccess = false, Message = "Неверный текущий пароль" };
        var now = clock.GetUtcNow().UtcDateTime;
        await users.ChangeUserPasswordAsync(id, passwords.HashPassword(request.NewPassword, passwords.GenerateSalt()), now);

        return new ChangeUserPasswordResponse { IsSuccess = true, Message = "Пароль изменён" };
    }

    /// <summary>
    /// Возвращает профиль без парольных данных
    /// </summary>
    private static GetUserResponse Map(UserProfileDal user) => new()
    {
        Id = user.UserId, Email = user.Email ?? string.Empty,
        FirstName = user.FirstName, LastName = user.LastName, Patronymic = user.Patronymic
    };
}
