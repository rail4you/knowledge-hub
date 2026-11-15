using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Whiteboards;

/// <summary>
/// 白板 AI 助手的会话记录（按「白板 + 用户」隔离）。
///
/// <para><b>为什么要落库</b>：助手是靠上下文干活的 —— 老师刷新页面、换设备、
/// 第二天接着备课，都要能承接「再具体点」「把第 2 条写成提问」这类追问。
/// 只放在前端内存里，一次刷新上下文就没了。</para>
///
/// <para><b>为什么不复用协作聊天</b>：协作频道是多人共享的，助手对话是老师私人的备课过程；
/// 混在一张表里既会串味（别人看到你跟 AI 说的话），也会让协作消息的未读/系统提示逻辑变复杂。</para>
/// </summary>
public class WhiteboardAssistantMessage : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid WhiteboardId { get; set; }

    /// <summary>会话归属人：每个老师只看到自己的助手记录。</summary>
    public Guid UserId { get; set; }

    /// <summary>user / assistant。</summary>
    public string Role { get; set; } = "user";

    public string Content { get; set; } = string.Empty;

    /// <summary>配图模式的图片地址（相对路径，如 /uploads/...）。</summary>
    public string? ImageUrl { get; set; }

    /// <summary>配图模式使用的画面描述。</summary>
    public string? ImagePrompt { get; set; }

    private WhiteboardAssistantMessage()
    {
    }

    public WhiteboardAssistantMessage(Guid id, Guid? tenantId, Guid whiteboardId, Guid userId,
        string role, string content, string? imageUrl = null, string? imagePrompt = null)
        : base(id)
    {
        TenantId = tenantId;
        WhiteboardId = whiteboardId;
        UserId = userId;
        Role = role;
        Content = content;
        ImageUrl = imageUrl;
        ImagePrompt = imagePrompt;
    }
}
