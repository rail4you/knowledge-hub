using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Whiteboards;

/// <summary>
/// 教研白板（聚合根）。
///
/// <para>用于「教师教研」场景：一名教师创建白板并填写主题，邀请同租户其他教师加入共创，
/// 多人同时打开同一白板即可实时互相看到编辑内容与光标。</para>
///
/// <para>元素不放在聚合内持久化（画布改动频繁、单元素粒度同步），
/// 而是由 <see cref="WhiteboardElement"/> 独立存储，聚合只保留元信息与视口。</para>
/// </summary>
public class Whiteboard : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>教研主题（如「七年级语文《春》单元教学设计」）。</summary>
    public string? Topic { get; set; }

    /// <summary>学科 / 年级（自由文本，如「语文 · 七年级」）。</summary>
    public string? Subject { get; set; }

    public string? Description { get; set; }

    // 注意：不要在这里再声明 CreatorId —— FullAuditedAggregateRoot 已有 Guid? CreatorId，
    // 子类同名字段会被 EF 判成「Guid 不可空但被当作可空」（项目里 Resource 踩过同一个坑）。
    // 创建者统一用基类 CreatorId。

    public string CreatorName { get; set; } = string.Empty;

    public WhiteboardStatus Status { get; set; } = WhiteboardStatus.Active;

    /// <summary>最近一次画布视口（平移/缩放），供新加入者对齐视角。</summary>
    public double ViewX { get; set; }

    public double ViewY { get; set; }

    public double ViewScale { get; set; } = 1d;

    public ICollection<WhiteboardMember> Members { get; set; } = new List<WhiteboardMember>();

    private Whiteboard()
    {
    }

    public Whiteboard(Guid id, Guid? tenantId, string title, Guid creatorId, string creatorName)
        : base(id)
    {
        TenantId = tenantId;
        Title = title.Trim();
        CreatorId = creatorId;   // 基类字段
        CreatorName = creatorName;
    }

    public void SetMeta(string? topic, string? subject, string? description)
    {
        Topic = topic;
        Subject = subject;
        Description = description;
    }

    public void Rename(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new UserFriendlyException("请填写白板标题。");
        }

        Title = title.Trim();
    }

    public void SetViewport(double x, double y, double scale)
    {
        ViewX = x;
        ViewY = y;
        ViewScale = scale <= 0 ? 1d : scale;
    }

    public void Archive() => Status = WhiteboardStatus.Archived;
}
