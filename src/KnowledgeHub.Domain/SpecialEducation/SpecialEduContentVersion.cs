using System;
using KnowledgeHub.SpecialEducation;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.SpecialEducation;

/// <summary>
/// 结构化内容版本快照（只增不改）：AI 生成记 v1，每次手动编辑自动 +1 并留档。
/// SnapshotJson 为 canonical 结构化 JSON（camelCase），前端按字段取历史值对比/采用。
/// </summary>
public class SpecialEduContentVersion : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public SpecialEduContentType ContentType { get; set; }
    public Guid EntityId { get; set; }
    public int VersionNumber { get; set; } = 1;
    public string Title { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = "{}";

    protected SpecialEduContentVersion() { }

    public SpecialEduContentVersion(Guid id, Guid? tenantId, SpecialEduContentType contentType, Guid entityId, int versionNumber) : base(id)
    {
        TenantId = tenantId;
        ContentType = contentType;
        EntityId = entityId;
        VersionNumber = versionNumber;
    }
}
