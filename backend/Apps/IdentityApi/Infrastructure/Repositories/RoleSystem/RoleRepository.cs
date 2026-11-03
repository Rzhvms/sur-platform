using Core.Database.DapperExtensions.EntityMapper;
using System.Data;
using Application.Ports.Repositories;
using Dapper;
using Domain.DalModels;

namespace Infrastructure.Repositories.RoleSystem;

/// <inheritdoc/>
public class RoleRepository(IDbConnection dbConnection) : IRoleRepository
{
    /// <inheritdoc/>
    public async Task<RoleDal?> GetRoleByNameAsync(string name)
    {
        return await dbConnection.QuerySingleOrDefaultAsync<RoleDal>(
            $"SELECT * FROM {EntityMapper.TbName<RoleDal>()} WHERE {EntityMapper.ColName<RoleDal>(x => x.Name)} = @name", new { name });
    }

    /// <inheritdoc/>
    public async Task<Guid[]> GetUserRightIdsAsync(Guid userId)
    {
        var rightIdList = await dbConnection.QueryAsync<Guid>($"""
            SELECT DISTINCT rr.{EntityMapper.ColName<RoleRightDal>(x => x.RightId)}
            FROM {EntityMapper.TbName<RoleRightDal>()} rr
            JOIN {EntityMapper.TbName<UserRoleDal>()} ur ON ur.{EntityMapper.ColName<RoleRightDal>(x => x.RoleId)} = rr.{EntityMapper.ColName<RoleRightDal>(x => x.RoleId)}
            WHERE ur.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = @userId
            """, new { userId });

        return rightIdList.ToArray();
    }
}
