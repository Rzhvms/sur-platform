using Domain.DalModels;
using Domain.DalModels.Users;

namespace Infrastructure.Repositories.Demo.Models;

/// <summary>Профиль демопользователя с привязкой к установленному системному каталогу</summary>
/// <param name="Profile">Профиль без секретов</param>
/// <param name="RoleCode">Системный код роли, чей ID разрешается внутри клиентской базы</param>
internal sealed record BankingDemoUser(UserProfileDal Profile, string RoleCode);
