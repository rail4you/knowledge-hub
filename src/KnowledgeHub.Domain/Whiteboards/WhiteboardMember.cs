using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Whiteboards;

/// <summary>白板成员（共创教师）。</summary>
public class WhiteboardMember : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid WhiteboardId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>用户名快照（离线也显示得出来）。</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>光标 / 头像配色（邀请时分配，保证同一白板内不重复）。</summary>
    public string Color { get; set; } = "#2f6fed";

    public WhiteboardMemberRole Role { get; set; } = WhiteboardMemberRole.Editor;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    private WhiteboardMember()
    {
    }

    public WhiteboardMember(Guid id, Guid? tenantId, Guid whiteboardId, Guid userId, string userName,
        string color, WhiteboardMemberRole role)
        : base(id)
    {
        TenantId = tenantId;
        WhiteboardId = whiteboardId;
        UserId = userId;
        UserName = userName;
        Color = color;
        Role = role;
    }
}
