using Infrastructure.Constants;
using Application.Constants;
using Domain.DalModels;
using Core.Database.DapperExtensions.EntityMapper;
using Application.Models;
using System.Data;
using Application.Ports.Repositories;
using Dapper;
using Domain.DalModels.Rights;
using Domain.DalModels.Roles;
using Domain.DalModels.Users;

namespace Infrastructure.Repositories.RoleSystem;

/// <inheritdoc/>
public class RoleRepository(IDbConnection dbConnection) : IRoleRepository
{
    /// <inheritdoc/>
    public Task<IdentityAccess?> GetAccessAsync(Guid userId)
    {
        var admin = Guid.Parse(IdentityRights.Admin);
        var selectUserRoleQuery = $"""
            SELECT u.{EntityMapper.ColName<UserProfileDal>(x => x.AccessVersion)}, u.{EntityMapper.ColName<UserProfileDal>(x => x.MustChangePassword)}, u.{EntityMapper.ColName<UserProfileDal>(x => x.TemporaryPasswordExpiresAt)},
                access.ids AS {EntityMapper.ColName<IdentityAccess>(x => x.RightIds)},
                CASE WHEN {EntityMapper.ParameterName(nameof(admin))} = ANY(access.ids)
                    THEN ARRAY(SELECT p.{EntityMapper.ColName<RightDal>(x => x.Code)} FROM {EntityMapper.TbName<RightDal>()} p
                        WHERE p.{EntityMapper.ColName<RightDal>(x => x.Code)} IS NOT NULL ORDER BY p.{EntityMapper.ColName<RightDal>(x => x.Code)})
                    ELSE access.codes END AS {EntityMapper.ColName<IdentityAccess>(x => x.RightCodes)}
            FROM {EntityMapper.TbName<UserProfileDal>()} u
            CROSS JOIN LATERAL (
                SELECT COALESCE(array_agg(DISTINCT p.{EntityMapper.ColName<RightDal>(x => x.Id)}), ARRAY[]::uuid[]) AS ids,
                    COALESCE(array_agg(DISTINCT p.{EntityMapper.ColName<RightDal>(x => x.Code)} ORDER BY p.{EntityMapper.ColName<RightDal>(x => x.Code)}), ARRAY[]::text[]) AS codes
                FROM {EntityMapper.TbName<UserRoleDal>()} a
                JOIN {EntityMapper.TbName<RoleDal>()} r ON r.{EntityMapper.ColName<RoleDal>(x => x.Id)} = a.{EntityMapper.ColName<UserRoleDal>(x => x.RoleId)}
                JOIN {EntityMapper.TbName<RoleRightDal>()} rr ON rr.{EntityMapper.ColName<RoleRightDal>(x => x.RoleId)} = r.{EntityMapper.ColName<RoleDal>(x => x.Id)}
                JOIN {EntityMapper.TbName<RightDal>()} p ON p.{EntityMapper.ColName<RightDal>(x => x.Id)} = rr.{EntityMapper.ColName<RoleRightDal>(x => x.RightId)}
                WHERE a.{EntityMapper.ColName<UserRoleDal>(x => x.UserId)} = u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)}
                    AND a.{EntityMapper.ColName<UserRoleDal>(x => x.IsActive)} AND r.{EntityMapper.ColName<RoleDal>(x => x.IsActive)}
                    AND r.{EntityMapper.ColName<RoleDal>(x => x.Code)} IS NOT NULL AND p.{EntityMapper.ColName<RightDal>(x => x.Code)} IS NOT NULL
            ) access
            WHERE u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = {EntityMapper.ParameterName(SqlParameterNames.UserId)} AND u.{EntityMapper.ColName<UserProfileDal>(x => x.IsActive)}
            """;
        return dbConnection.QuerySingleOrDefaultAsync<IdentityAccess>(selectUserRoleQuery, new { userId, admin });
    }
}
