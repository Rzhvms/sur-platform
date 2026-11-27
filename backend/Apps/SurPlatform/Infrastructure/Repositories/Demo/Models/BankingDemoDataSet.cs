using Domain.DalModels;
using Domain.DalModels.Assessments;
using Domain.DalModels.Organization;
using Domain.DalModels.Reviews;
using Domain.DalModels.Risks;
using Domain.DalModels.Treatments;

namespace Infrastructure.Repositories.Demo.Models;

/// <summary>Согласованный набор фиктивных данных банковского проекта</summary>
internal sealed record BankingDemoDataSet
{
    /// <summary>Название демонстрационного проекта</summary>
    public string ProjectName { get; init; } = string.Empty;

    /// <summary>Исходная дата для переноса календаря к первому запуску</summary>
    public DateTime ReferenceDate { get; init; }

    /// <summary>Идентификатор шкалы для клиента без опубликованных границ</summary>
    public Guid ScaleId { get; init; }

    /// <summary>Начальные денежные границы при отсутствии настройки клиента</summary>
    public decimal[] ScaleMinimums { get; init; } = [];

    /// <summary>Демонстрационные записи Users</summary>
    public BankingDemoUser[] Users { get; init; } = [];

    /// <summary>Демонстрационные записи Directorates</summary>
    public DirectorateDal[] Directorates { get; init; } = [];

    /// <summary>Демонстрационные записи Persons</summary>
    public OrgPersonDal[] Persons { get; init; } = [];

    /// <summary>Демонстрационные записи Processes</summary>
    public ProcessDal[] Processes { get; init; } = [];

    /// <summary>Демонстрационные записи Assets</summary>
    public AssetDal[] Assets { get; init; } = [];

    /// <summary>Демонстрационные записи HazardGroups</summary>
    public HazardGroupDal[] HazardGroups { get; init; } = [];

    /// <summary>Демонстрационные записи Hazards</summary>
    public HazardDal[] Hazards { get; init; } = [];

    /// <summary>Демонстрационные записи Categories</summary>
    public RiskCategoryDal[] Categories { get; init; } = [];

    /// <summary>Демонстрационные записи Responsibilities</summary>
    public ResponsibilityDal[] Responsibilities { get; init; } = [];

    /// <summary>Демонстрационные записи Risks</summary>
    public RiskDal[] Risks { get; init; } = [];

    /// <summary>Демонстрационные записи RiskProcesses</summary>
    public RiskProcessDal[] RiskProcesses { get; init; } = [];

    /// <summary>Демонстрационные записи Assessments</summary>
    public RiskAssessmentDal[] Assessments { get; init; } = [];

    /// <summary>Демонстрационные записи Treatments</summary>
    public TreatmentDal[] Treatments { get; init; } = [];

    /// <summary>Демонстрационные записи Assignments</summary>
    public TreatmentAssignmentDal[] Assignments { get; init; } = [];

    /// <summary>Демонстрационные записи RiskComments</summary>
    public RiskCommentDal[] RiskComments { get; init; } = [];

    /// <summary>Демонстрационные записи TreatmentComments</summary>
    public TreatmentCommentDal[] TreatmentComments { get; init; } = [];

    /// <summary>Демонстрационные записи ReviewRuns</summary>
    public ReviewRunDal[] ReviewRuns { get; init; } = [];

    /// <summary>Демонстрационные записи Reviews</summary>
    public RiskReviewDal[] Reviews { get; init; } = [];
}
