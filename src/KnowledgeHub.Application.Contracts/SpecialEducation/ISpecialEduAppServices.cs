using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.SpecialEducation.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.SpecialEducation;

public interface ISpecialTeachingDesignAppService : IApplicationService
{
    Task<PagedResultDto<SpecialTeachingDesignDto>> GetListAsync(GetTeachingDesignListInputDto input);
    Task<SpecialTeachingDesignDto> GetAsync(Guid id);
    Task<SpecialTeachingDesignDto> SaveAsync(SaveTeachingDesignInputDto input);
    Task<SpecialTeachingDesignDto> UpdateContentAsync(UpdateTeachingDesignContentDto input);
    Task<List<SpecialEduContentVersionDto>> GetVersionsAsync(Guid id);
    Task<SpecialTeachingDesignDto> SubmitForReviewAsync(SubmitTeachingDesignForReviewInputDto input);
    Task<SpecialTeachingDesignDto> ReviewAsync(ReviewTeachingDesignInputDto input);
    Task DeleteAsync(Guid id);
}

public interface ISpecialIepAppService : IApplicationService
{
    Task<PagedResultDto<IepPlanDto>> GetListAsync(GetIepListInputDto input);
    Task<IepPlanDto> GetAsync(Guid id);
    Task<IepPlanDto> SaveAsync(SaveIepInputDto input);
    Task<IepPlanDto> UpdateContentAsync(UpdateIepContentDto input);
    Task<List<SpecialEduContentVersionDto>> GetVersionsAsync(Guid id);
    Task<IepPlanDto> SubmitForReviewAsync(SubmitIepForReviewInputDto input);
    Task<IepPlanDto> ReviewAsync(ReviewIepInputDto input);
    Task DeleteAsync(Guid id);
    Task<PagedResultDto<IepPlanDto>> GetMyIepListAsync(GetIepListInputDto input);
}

public interface ISpecialEduResourceAppService : IApplicationService
{
    Task<PagedResultDto<SpecialEduResourceDto>> GetListAsync(GetSpecialResourceListInputDto input);
    Task<SpecialEduResourceDto> GetAsync(Guid id);
    Task<SpecialEduResourceDto> SaveAsync(SaveSpecialResourceInputDto input);
    Task<SpecialEduResourceDto> UpdateContentAsync(UpdateResourceContentDto input);
    Task<List<SpecialEduContentVersionDto>> GetVersionsAsync(Guid id);
    Task<SpecialEduResourceDto> SubmitForReviewAsync(SubmitSpecialResourceForReviewInputDto input);
    Task<SpecialEduResourceDto> ReviewAsync(ReviewSpecialResourceInputDto input);
    Task DeleteAsync(Guid id);
}

public interface ISpecialEduOptionAppService : IApplicationService
{
    Task<List<SpecialEduCourseOptionDto>> GetCourseOptionsAsync();
    Task<List<SpecialEduStudentOptionDto>> GetStudentOptionsAsync(GetSpecialEduStudentOptionsInput input);
    Task<List<SpecialEduTeacherOptionDto>> GetTeacherOptionsAsync();
}

public interface ISpecialEducationAdminAppService : IApplicationService
{
    Task<List<SpecialEduTenantStateDto>> GetTenantStatesAsync();
    Task SetTenantEnabledAsync(SetSpecialEduTenantEnabledDto input);
    Task<SeedMockDataResultDto> SeedMockDataAsync(SeedMockDataInputDto input);
}

public class SeedMockDataResultDto
{
    public int TeachingDesignCount { get; set; }
    public int IepCount { get; set; }
    public int ResourceCount { get; set; }
    public string Message { get; set; } = string.Empty;
}
