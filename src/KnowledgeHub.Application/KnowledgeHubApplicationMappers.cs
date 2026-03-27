using KnowledgeHub.Users;
using KnowledgeHub.Resources;
using KnowledgeHub.Courses;
using KnowledgeHub.Courses.Dtos;
using KnowledgeHub.Alliance;
using KnowledgeHub.Application.Contracts.Alliance;
using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace KnowledgeHub;

[Mapper]
public partial class ResourceToResourceDtoMapper : MapperBase<Resource, ResourceDto>
{
    public override partial ResourceDto Map(Resource source);

    public override partial void Map(Resource source, ResourceDto destination);
}

[Mapper]
public partial class CreateUpdateResourceDtoToResourceMapper : MapperBase<CreateUpdateResourceDto, Resource>
{
    public override partial Resource Map(CreateUpdateResourceDto source);

    public override partial void Map(CreateUpdateResourceDto source, Resource destination);
}

[Mapper]
public partial class ResourceVersionToResourceVersionDtoMapper : MapperBase<ResourceVersion, ResourceVersionDto>
{
    public override partial ResourceVersionDto Map(ResourceVersion source);

    public override partial void Map(ResourceVersion source, ResourceVersionDto destination);
}

[Mapper]
public partial class ResourceCategoryToResourceCategoryDtoMapper : MapperBase<ResourceCategory, ResourceCategoryDto>
{
    public override partial ResourceCategoryDto Map(ResourceCategory source);

    public override partial void Map(ResourceCategory source, ResourceCategoryDto destination);
}

[Mapper]
public partial class ResourceAuditToResourceAuditDtoMapper : MapperBase<ResourceAudit, ResourceAuditDto>
{
    public override partial ResourceAuditDto Map(ResourceAudit source);

    public override partial void Map(ResourceAudit source, ResourceAuditDto destination);
}

[Mapper]
public partial class ChapterToChapterDtoMapper : MapperBase<Chapter, ChapterDto>
{
    public override partial ChapterDto Map(Chapter source);

    public override partial void Map(Chapter source, ChapterDto destination);
}

[Mapper]
public partial class ChapterDtoToChapterMapper : MapperBase<ChapterDto, Chapter>
{
    public override partial Chapter Map(ChapterDto source);

    public override partial void Map(ChapterDto source, Chapter destination);
}

[Mapper]
public partial class CourseToCourseDtoMapper : MapperBase<Course, CourseDto>
{
    public override partial CourseDto Map(Course source);

    public override partial void Map(Course source, CourseDto destination);
}

[Mapper]
public partial class CreateUpdateCourseDtoToCourseMapper : MapperBase<CreateUpdateCourseDto, Course>
{
    public override partial Course Map(CreateUpdateCourseDto source);

    public override partial void Map(CreateUpdateCourseDto source, Course destination);
}

[Mapper]
public partial class AllianceToAllianceDtoMapper : MapperBase<KnowledgeHub.Alliance.Alliance, AllianceDto>
{
    public override partial AllianceDto Map(KnowledgeHub.Alliance.Alliance source);

    public override partial void Map(KnowledgeHub.Alliance.Alliance source, AllianceDto destination);
}

[Mapper]
public partial class AllianceMemberToAllianceMemberDtoMapper : MapperBase<KnowledgeHub.Alliance.AllianceMember, AllianceMemberDto>
{
    public override partial AllianceMemberDto Map(KnowledgeHub.Alliance.AllianceMember source);

    public override partial void Map(KnowledgeHub.Alliance.AllianceMember source, AllianceMemberDto destination);
}

[Mapper]
public partial class AllianceAuditToAllianceAuditDtoMapper : MapperBase<KnowledgeHub.Alliance.AllianceAudit, AllianceAuditDto>
{
    public override partial AllianceAuditDto Map(KnowledgeHub.Alliance.AllianceAudit source);

    public override partial void Map(KnowledgeHub.Alliance.AllianceAudit source, AllianceAuditDto destination);
}
