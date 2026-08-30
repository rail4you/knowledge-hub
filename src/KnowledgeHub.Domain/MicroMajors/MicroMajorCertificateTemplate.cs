using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.MicroMajors;

/// <summary>
/// 微专业证书模板：由管理员预先上传到微专业下，
/// 发证时可直接选择，无需每次重新上传证书图片。
/// </summary>
public class MicroMajorCertificateTemplate : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid MicroMajorId { get; set; }
    /// <summary>模板名称，如「结业证书」「优秀学员证书」</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>证书图片 URL（由 OSS 上传后得到）</summary>
    public string ImageUrl { get; set; } = string.Empty;
    /// <summary>排序，数值小的排前面</summary>
    public int SortOrder { get; set; }
    /// <summary>占位符图层配置（JSON 数组），发证时在证书图片上叠加姓名、学号、发证时间等字段</summary>
    public string? LayersJson { get; set; }

    /// <summary>
    /// 占位符字段类型（与前端约定）。
    /// 用于在模板编辑器中拖放图层并映射到实际数据。
    /// </summary>
    public const string FieldStudentName = "studentName";
    public const string FieldStudentNo = "studentNo";
    public const string FieldAdvisor = "advisor";
    public const string FieldIssueDate = "issueDate";
    public const string FieldCertificateNo = "certificateNo";
    public const string FieldValidUntil = "validUntil";
    public const string FieldMicroMajorTitle = "microMajorTitle";
    public const string FieldCustom = "custom";

    public MicroMajorCertificateTemplate()
    {
    }

    public MicroMajorCertificateTemplate(Guid id, Guid microMajorId, string name, string imageUrl)
        : base(id)
    {
        MicroMajorId = microMajorId;
        Name = name;
        ImageUrl = imageUrl;
    }
}