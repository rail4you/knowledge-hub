using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Whiteboards;

/// <summary>
/// 白板协作消息（右侧聊天侧边栏）。
///
/// <para><b>为什么消息要落库</b>：语音 / 文字协作是「开会」场景，教师刷新页面、断线重连、
/// 后来加入都要能看到之前的讨论结论，纯内存广播会一刷新就丢。</para>
///
/// <para><b><see cref="ClientId"/></b>：前端发送时带的临时 Id。服务端原样回传，
/// 前端用它把「本地乐观插入的气泡」替换成落库版本，避免同一条消息出现两次。</para>
///
/// <para><b><see cref="Kind"/></b>：普通发言 / 系统提示（“X 加入了语音通话”），
/// 系统提示也落库，才能和历史发言保持同一时间线。</para>
/// </summary>
public class WhiteboardChatMessage : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid WhiteboardId { get; set; }

    public Guid SenderId { get; set; }

    public string SenderName { get; set; } = string.Empty;

    /// <summary>发送人光标配色（侧边栏头像直接用，免去前端再算一次）。</summary>
    public string? SenderColor { get; set; }

    /// <summary>前端临时消息 Id（可空，历史数据/系统消息没有）。</summary>
    public string? ClientId { get; set; }

    public string Text { get; set; } = string.Empty;

    public WhiteboardChatMessageKind Kind { get; set; } = WhiteboardChatMessageKind.Text;

    private WhiteboardChatMessage()
    {
    }

    public WhiteboardChatMessage(Guid id, Guid? tenantId, Guid whiteboardId, Guid senderId,
        string senderName, string text, WhiteboardChatMessageKind kind = WhiteboardChatMessageKind.Text,
        string? senderColor = null, string? clientId = null)
        : base(id)
    {
        TenantId = tenantId;
        WhiteboardId = whiteboardId;
        SenderId = senderId;
        SenderName = senderName;
        Text = text;
        Kind = kind;
        SenderColor = senderColor;
        ClientId = clientId;
    }
}
