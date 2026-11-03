using Core.Database.DapperExtensions.EntityMapper;
using System.Data;
using Application.Ports.Repositories;
using Core.Abstractions.Exceptions;
using Dapper;
using Domain.DalModels;

namespace Infrastructure.Repositories.RoleSystem;

/// <inheritdoc/>
public class RoleRepository(IDbConnection dbConnection) : IRoleRepository
{
    /// <inheritdoc/>
    public async Task<RoleDal?> GetRoleByNameAsync(string name)
    {
        var searchQuery = $"""
                           SELECT * FROM {EntityMapper.TbName<RoleDal>()}
                           WHERE {EntityMapper.ColName<RoleDal>(x => x.Name)} = {EntityMapper.ParameterPrefix}{nameof(name)}
                           """;
        
        var result = await dbConnection.QuerySingleOrDefaultAsync<RoleDal>(searchQuery, new { name });
        
        if (result is null)
        {
            throw new EntityNotFoundException($"Роль с названием {name} не найдена");
        }
        
        return result;
    }

    /// <inheritdoc/>
    public async Task<List<Guid>> GetUserRightIdListAsync(Guid userId)
    {
        var searchQuery = $"""
                           SELECT rr.{EntityMapper.ColName<RoleRightDal>(x => x.RightId)}
                           FROM {EntityMapper.TbName<RoleRightDal>()} rr
                           JOIN {EntityMapper.TbName<UserRoleDal>()} ur 
                           ON ur.{EntityMapper.ColName<RoleRightDal>(x => x.RoleId)} = 
                              rr.{EntityMapper.ColName<UserRoleDal>(x => x.RoleId)}
                           WHERE ur.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = {EntityMapper.ParameterPrefix}{nameof(userId)}
                           """;
        
        var rightIdList = await dbConnection.QueryAsync<Guid>(searchQuery, new { userId });

        return rightIdList.AsList();
    }
}