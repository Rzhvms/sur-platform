using Application.Helpers.Encryption;
using Application.Ports.Repositories;
using Application.UseCases.Users.Dto.Request;
using Application.UseCases.Users.Dto.Response;
using Application.UseCases.Users.Interfaces;
using Domain.DalModels;

namespace Application.UseCases.Users;

/// <inheritdoc />
internal sealed class UserUseCaseManager(IUserRepository userRepository) : IUserUseCaseManager
{
    /// <inheritdoc/>
    public async Task<GetUserListResponse> GetUserListAsync()
    {
        var userList = await userRepository.GetUserProfilesAsync();
        var responseModel = userList.Select(Map).ToList();
        
        return new GetUserListResponse { UserList = responseModel };
    }

    /// <inheritdoc/>
    public async Task<GetUserResponse> GetUserByIdAsync(Guid userId)
    {
        var user = await userRepository.GetUserByUserIdAsync(userId);
        return Map(user);
    }

    /// <inheritdoc/>
    public async Task DeleteUserByIdAsync(Guid userId)
    {
        await userRepository.DeleteUserByIdAsync(userId);
    }

    /// <inheritdoc/>
    public async Task<ChangeUserPasswordResponse> ChangePasswordAsync(Guid id, ChangeUserPasswordRequest request)
    {
        var credential = await userRepository.GetCredentialAsync(id);

        var isAccess = PasswordEncryptionHelper.VerifyPassword(request.OldPassword, credential.Password);

        if (!isAccess)
        {
            return new ChangeUserPasswordResponse
            {
                IsSuccess = false, 
                Message = "Неверный текущий пароль"
            };
        }
        
        var currentDateTime = DateTime.UtcNow;
        var hashedSalt = PasswordEncryptionHelper.GenerateSalt();
        var hashedPassword = PasswordEncryptionHelper.HashPassword(request.NewPassword, hashedSalt);
        
        await userRepository.ChangeUserPasswordAsync(id, hashedPassword, currentDateTime);

        return new ChangeUserPasswordResponse
        {
            IsSuccess = true, 
            Message = "Пароль изменён"
        };
    }

    /// <summary>
    /// Формирование response-модели профиля пользователя
    /// </summary>
    private static GetUserResponse Map(UserProfileDal user) => new()
    {
        Id = user.UserId, 
        Email = user.Email ?? string.Empty,
        FirstName = user.FirstName, 
        LastName = user.LastName, 
        Patronymic = user.Patronymic
    };
}