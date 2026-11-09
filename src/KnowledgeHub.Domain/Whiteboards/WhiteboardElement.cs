using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Whiteboards;

/// <summary>
/// 画布元素（文本 / 便签 / 图片 / 视频）。
///
/// <para><see cref="ElementKey"/> 由前端生成并在整块白板内唯一，
/// 实时协同按这个键做 upsert 定位，避免「服务端 Guid 与前端本地 id 对不上」。</para>
///
/// <para><see cref="Json"/> 存元素完整属性（type/x/y/w/h/rot/text/fs/fill/bg/resourceId…），
/// 前端直接反序列化即可重建 Konva 节点；服务端不解析具体字段，
/// 只在需要时取 <see cref="ElementType"/> 做统计。</para>
/// </summary>
public class WhiteboardElement : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid WhiteboardId { get; set; }

    public string ElementKey { get; set; } = string.Empty;

    public WhiteboardElementType ElementType { get; set; }

    public string Json { get; set; } = "{}";

    public Guid? LastEditorId { get; set; }

    public string? LastEditorName { get; set; }

    private WhiteboardElement()
    {
    }

    public WhiteboardElement(Guid id, Guid? tenantId, Guid whiteboardId, string elementKey,
        WhiteboardElementType elementType, string json)
        : base(id)
    {
        TenantId = tenantId;
        WhiteboardId = whiteboardId;
        ElementKey = elementKey;
        ElementType = elementType;
        Json = json;
    }

    public void Update(string json, WhiteboardElementType elementType, Guid? editorId, string? editorName)
    {
        Json = json;
        ElementType = elementType;
        LastEditorId = editorId;
        LastEditorName = editorName;
    }
}
