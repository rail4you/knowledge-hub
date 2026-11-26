using System;
using System.Text.Json;
using System.Threading.Tasks;
using KnowledgeHub.Learning.Enums;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace KnowledgeHub.Learning;

/// <summary>
/// 学生行为埋点。**设计为"永不影响主流程"**：内部吞掉所有异常并只记 warning，
/// 因为行为日志是派生数据，写失败不能让作答/交卷/签到这些用户动作失败。
/// </summary>
public interface IStudentActivityLogger
{
    Task LogAsync(
        Guid studentId,
        StudentActivityAction action,
        Guid? courseId = null,
        string? resourceType = null,
        Guid? resourceId = null,
        object? metadata = null);
}

public class StudentActivityLogger : IStudentActivityLogger, ITransientDependency
{
    private readonly IRepository<StudentActivityLog, Guid> _repository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<StudentActivityLogger> _logger;

    public StudentActivityLogger(
        IRepository<StudentActivityLog, Guid> repository,
        IGuidGenerator guidGenerator,
        ILogger<StudentActivityLogger> logger)
    {
        _repository = repository;
        _guidGenerator = guidGenerator;
        _logger = logger;
    }

    public async Task LogAsync(
        Guid studentId,
        StudentActivityAction action,
        Guid? courseId = null,
        string? resourceType = null,
        Guid? resourceId = null,
        object? metadata = null)
    {
        if (studentId == Guid.Empty)
        {
            return;
        }

        try
        {
            var log = new StudentActivityLog(_guidGenerator.Create(), studentId, action)
            {
                CourseId = courseId,
                ResourceType = resourceType,
                ResourceId = resourceId,
                Metadata = metadata == null ? null : JsonSerializer.Serialize(metadata)
            };

            await _repository.InsertAsync(log, autoSave: true);
        }
        catch (Exception ex)
        {
            // 埋点失败只记日志，绝不抛出
            _logger.LogWarning(ex, "学生行为埋点失败 studentId={StudentId} action={Action}", studentId, action);
        }
    }
}
