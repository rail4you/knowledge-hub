using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Assignments.Dtos;
using Microsoft.AspNetCore.Http;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Assignments;

/// <summary>
/// 课程作业：教师发布 / 批阅，学生提交 / 查看成绩反馈。
///
/// 教师端接口统一挂在 <c>/api/app/assignment</c>，学生端接口带 <c>my-</c> 前缀，
/// 与考试模块（ExamAppService）的路由风格保持一致。
/// </summary>
public interface IAssignmentAppService : IApplicationService
{
    // ── 教师端：作业管理 ───────────────────────────────────
    Task<PagedResultDto<AssignmentListItemDto>> GetListAsync(AssignmentListQueryDto input);

    /// <summary>课程维度统计卡片（进行中 / 待批阅 / 平均分 / 迟交）。</summary>
    Task<AssignmentStatisticsDto> GetStatisticsAsync(Guid courseId);

    Task<AssignmentDetailDto> GetAsync(Guid id);

    Task<AssignmentDetailDto> CreateAsync(CreateUpdateAssignmentDto input);

    Task<AssignmentDetailDto> UpdateAsync(Guid id, CreateUpdateAssignmentDto input);

    /// <summary>发布作业（草稿 → 已发布）。</summary>
    Task<AssignmentDetailDto> PublishAsync(Guid id);

    /// <summary>结束作业（不再接受提交）。</summary>
    Task<AssignmentDetailDto> CloseAsync(Guid id);

    Task DeleteAsync(Guid id);

    /// <summary>导出成绩（CSV 文本）。</summary>
    Task<string> GetExportScoresAsync(Guid assignmentId);

    // ── 教师端：批阅 ───────────────────────────────────────
    Task<PagedResultDto<AssignmentSubmissionItemDto>> GetSubmissionsAsync(AssignmentSubmissionQueryDto input);

    Task<AssignmentSubmissionDetailDto> GetSubmissionAsync(Guid submissionId);

    /// <summary>批阅：保存得分明细 + 总分 + 评语。</summary>
    Task<AssignmentSubmissionDetailDto> GradeAsync(Guid submissionId, GradeAssignmentSubmissionDto input);

    // ── 文件 ──────────────────────────────────────────────
    /// <summary>上传文件（作业要求附件 / 学生提交件 / 教师批注版）。</summary>
    Task<AssignmentFileDto> UploadFileAsync(Guid assignmentId, IFormFile file, string? purpose = null);

    /// <summary>下载 / 预览文件（返回文件字节）。</summary>
    Task<byte[]> GetFileAsync(Guid fileId);

    // ── 学生端 ─────────────────────────────────────────────
    Task<List<StudentAssignmentItemDto>> GetMyAssignmentsAsync(Guid courseId);

    Task<StudentAssignmentDetailDto> GetMyAssignmentAsync(Guid assignmentId);

    /// <summary>保存草稿 / 正式提交。</summary>
    Task<StudentAssignmentDetailDto> SubmitAsync(StudentSubmitAssignmentDto input);

    /// <summary>成绩与反馈。</summary>
    Task<StudentAssignmentFeedbackDto> GetMyFeedbackAsync(Guid assignmentId);
}
