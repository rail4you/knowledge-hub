using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace KnowledgeHub.AI;

/// <summary>
/// 教学场景模板：图片生成 / 视频首帧 / 视频运镜三类。
/// - 系统内置（<see cref="IsSystem"/>=true，TenantId 为空）：只读参考模板，所有租户可见，可复制为“我的场景”。
/// - 租户自定义（TenantId 为当前租户）：租户内可增删改。
/// </summary>
public class TeachingScene : FullAuditedEntity<Guid>
{
    /// <summary>所属租户；系统模板为 null。</summary>
    public Guid? TenantId { get; set; }

    /// <summary>场景名称，如“课堂讲解”。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>提示词正文。</summary>
    public string Prompt { get; set; } = string.Empty;

    public TeachingSceneCategory Category { get; set; }

    /// <summary>排序（升序）。</summary>
    public int SortOrder { get; set; }

    /// <summary>系统内置参考模板：只读，不可直接编辑/删除。</summary>
    public bool IsSystem { get; set; }

    protected TeachingScene()
    {
    }

    public TeachingScene(Guid id, string name, string prompt, TeachingSceneCategory category)
        : base(id)
    {
        Name = name;
        Prompt = prompt;
        Category = category;
    }
}

/// <summary>教学场景分类。</summary>
public enum TeachingSceneCategory : byte
{
    /// <summary>图片生成场景。</summary>
    Image = 0,

    /// <summary>视频首帧画面场景。</summary>
    VideoScene = 10,

    /// <summary>视频运镜 / 动作。</summary>
    VideoMotion = 20
}
