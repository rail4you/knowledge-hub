using System;
using System.Collections.Generic;

namespace KnowledgeHub.SpecialEducation.Dtos;

public class SpecialEduCourseOptionDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
}

public class SpecialEduStudentOptionDto
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool EnrolledInSelectedCourse { get; set; }
}

public class GetSpecialEduStudentOptionsInput
{
    public Guid? CourseId { get; set; }
}

public class SpecialEduTeacherOptionDto
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
}

public class SeedMockDataInputDto
{
    /// <summary>为空则写入当前租户；全局管理员可指定租户。</summary>
    public Guid? TenantId { get; set; }
}
