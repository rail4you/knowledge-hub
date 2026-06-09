using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Courses.Dtos;

public interface IKnowledgeResourceAppService : IApplicationService
{
    Task<List<KnowledgeResourceDto>> GetByCourseAsync(Guid courseId);
    Task<List<KnowledgeResourceDto>> GetByChapterAsync(Guid chapterId);
    Task<RelatedCoursesResultDto> GetRelatedCoursesAsync(Guid knowledgeResourceId);
    Task<KnowledgeResourceDto> CreateAsync(CreateUpdateKnowledgeResourceDto input);
    Task<KnowledgeResourceDto> UpdateAsync(Guid id, CreateUpdateKnowledgeResourceDto input);
    Task DeleteAsync(Guid id);
}

public class RelatedCoursesResultDto
{
    public Guid KnowledgeResourceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<RelatedCourseInfoDto> Courses { get; set; } = new();
}

public class RelatedCourseInfoDto
{
    public Guid CourseId { get; set; }
    public List<RelatedChapterInfoDto> Chapters { get; set; } = new();
}

public class RelatedChapterInfoDto
{
    public Guid ChapterId { get; set; }
    public string ChapterTitle { get; set; } = string.Empty;
}
