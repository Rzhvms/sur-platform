using Domain.DalModels;

namespace Application.Models;

/// <summary>
/// Организационные сведения для подтверждения деактивации учётной записи
/// </summary>
/// <param name="Person">Действующая запись сотрудника, если есть</param>
/// <param name="Processes">Активные процессы во владении</param>
/// <param name="Responsibilities">Активные назначения сотрудника</param>
public sealed record OrganizationUserContext(OrgPersonDal? Person, ProcessDal[] Processes, ResponsibilityDal[] Responsibilities);
