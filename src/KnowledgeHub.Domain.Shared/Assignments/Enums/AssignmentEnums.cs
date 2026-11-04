namespace KnowledgeHub.Assignments.Enums;

/// <summary>作业类型：个人作业 / 小组作业。</summary>
public enum AssignmentType
{
    /// <summary>个人作业</summary>
    Individual = 0,

    /// <summary>小组作业</summary>
    Group = 1
}

/// <summary>作业状态：草稿 → 已发布 → 已结束。</summary>
public enum AssignmentStatus
{
    /// <summary>草稿（未发布，学生看不到）</summary>
    Draft = 0,

    /// <summary>已发布（学生可提交）</summary>
    Published = 1,

    /// <summary>已结束（截止后教师手动关闭，不再接受提交）</summary>
    Closed = 2
}

/// <summary>提交状态：草稿 → 已提交 → 已批阅。</summary>
public enum SubmissionStatus
{
    /// <summary>草稿（学生保存但未正式提交）</summary>
    Draft = 0,

    /// <summary>已提交（等待教师批阅）</summary>
    Submitted = 1,

    /// <summary>已批阅（教师已给分）</summary>
    Graded = 2
}

/// <summary>作业文件的用途。</summary>
public enum AssignmentFileType
{
    /// <summary>作业要求附件（教师上传，学生下载）</summary>
    Requirement = 0,

    /// <summary>学生提交附件</summary>
    Submission = 1,

    /// <summary>教师批注版附件（批阅后回传给学生）</summary>
    Annotated = 2
}
