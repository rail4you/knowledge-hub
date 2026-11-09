using System;
using System.Collections.Generic;

namespace KnowledgeHub.Whiteboards.Dtos;

/// <summary>白板列表项。</summary>
public class WhiteboardListItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Topic { get; set; }
    public string? Subject { get; set; }
    public string CreatorName { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    public int ElementCount { get; set; }
    public bool IsOwner { get; set; }
    public DateTime CreationTime { get; set; }
    public DateTime? LastModificationTime { get; set; }
}

/// <summary>白板成员。</summary>
public class WhiteboardMemberDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Color { get; set; } = "#2f6fed";
    public int Role { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }

    /// <summary>当前是否在线（在线状态由 Hub 内存维护，随快照下发）。</summary>
    public bool Online { get; set; }
}

/// <summary>画布元素（前端直接消费，服务端不拆分字段）。</summary>
public class WhiteboardElementDto
{
    public string Key { get; set; } = string.Empty;
    /// <summary>元素完整 JSON（type/x/y/w/h/rot/text/fs/fill/bg/resourceId…）。</summary>
    public string Json { get; set; } = "{}";
    public int ElementType { get; set; }
    public string? LastEditorName { get; set; }
    public DateTime? LastModificationTime { get; set; }
}

/// <summary>白板详情（进入房间时一次性拉全量，之后走实时增量）。</summary>
public class WhiteboardDetailDto : WhiteboardListItemDto
{
    public string? Description { get; set; }
    public int Status { get; set; }
    public Guid? CreatorId { get; set; }
    public double ViewX { get; set; }
    public double ViewY { get; set; }
    public double ViewScale { get; set; } = 1d;
    public List<WhiteboardMemberDto> Members { get; set; } = new();
    public List<WhiteboardElementDto> Elements { get; set; } = new();

    /// <summary>当前登录人可编辑（Owner / Editor）。</summary>
    public bool CanEdit { get; set; }

    /// <summary>当前登录人的配色（用于本地光标）。</summary>
    public string MyColor { get; set; } = "#2f6fed";

    public string MyName { get; set; } = string.Empty;
}

/// <summary>创建白板。</summary>
public class CreateWhiteboardDto
{
    public string Title { get; set; } = string.Empty;
    public string? Topic { get; set; }
    public string? Subject { get; set; }
    public string? Description { get; set; }
}

/// <summary>修改白板元信息。</summary>
public class UpdateWhiteboardDto
{
    public string? Title { get; set; }
    public string? Topic { get; set; }
    public string? Subject { get; set; }
    public string? Description { get; set; }
}

/// <summary>邀请共创教师。</summary>
public class InviteWhiteboardMembersDto
{
    public List<Guid> UserIds { get; set; } = new();
    /// <summary>角色：1 共创者（默认）/ 2 只读。</summary>
    public int Role { get; set; } = 1;
}

/// <summary>可邀请的教师（同租户内，排除已加入）。</summary>
public class InvitableTeacherDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? Name { get; set; }
    public bool Joined { get; set; }
}
