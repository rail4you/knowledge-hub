using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.MicroMajors.Dtos;

/// <summary>
/// 证书模板占位符图层。
/// 在证书图片上用画布叠加姓名、学号、发证时间等字段，可拖放定位。
/// </summary>
public class CertificateTemplateLayerDto
{
    /// <summary>图层唯一标识（前端生成）</summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>字段类型：studentName / studentNo / advisor / issueDate / certificateNo / validUntil / microMajorTitle / custom</summary>
    public string FieldType { get; set; } = string.Empty;
    /// <summary>图层显示名称（如「姓名」「学号」）</summary>
    public string Label { get; set; } = string.Empty;
    /// <summary>自定义字段名称（仅当 FieldType == custom 时使用）</summary>
    public string? CustomFieldName { get; set; }
    /// <summary>水平位置（图片宽度的百分比 0-100）</summary>
    public double X { get; set; }
    /// <summary>垂直位置（图片高度的百分比 0-100）</summary>
    public double Y { get; set; }
    /// <summary>字体大小（相对 1000px 参考宽，按实际图片宽度缩放）</summary>
    public double FontSize { get; set; } = 40;
    /// <summary>字体颜色，如 #1f1f1f</summary>
    public string Color { get; set; } = "#1f1f1f";
    /// <summary>字重，如 400 / 600 / 700</summary>
    public int FontWeight { get; set; } = 600;
    /// <summary>是否相对 X 水平居中（否则左对齐）</summary>
    public bool Center { get; set; } = true;
    /// <summary>字体族，若为空则使用默认衬线/无衬线字体</summary>
    public string? FontFamily { get; set; }
}

public class MicroMajorCertificateTemplateDto : FullAuditedEntityDto<Guid>
{
    public Guid MicroMajorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    /// <summary>占位符图层配置（模板编辑器中拖放保存）</summary>
    public List<CertificateTemplateLayerDto> Layers { get; set; } = new();
}

public class CreateUpdateMicroMajorCertificateTemplateDto
{
    public Guid MicroMajorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    /// <summary>占位符图层配置（可空，未配置则仅使用原图）</summary>
    public List<CertificateTemplateLayerDto>? Layers { get; set; }
}