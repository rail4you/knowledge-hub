namespace KnowledgeHub.Studios;

/// <summary>名师工作室状态。</summary>
public enum StudioStatus : byte
{
    /// <summary>草稿（仅领衔人与管理员可见）。</summary>
    Draft = 0,

    /// <summary>启用（成员可上传资料）。</summary>
    Active = 1,

    /// <summary>停用（只读）。</summary>
    Disabled = 2,
}

/// <summary>工作室成员角色。</summary>
public enum StudioMemberRole : byte
{
    /// <summary>领衔人：管理成员、审核并发布资料。</summary>
    Leader = 0,

    /// <summary>成员：上传资料并提交审核。</summary>
    Member = 1,
}

/// <summary>工作室资料状态（简化的准入流：提交 → 领衔人审核 → 发布）。</summary>
public enum StudioMaterialStatus : byte
{
    /// <summary>草稿（上传者本人可见）。</summary>
    Draft = 0,

    /// <summary>待领衔人审核。</summary>
    PendingReview = 1,

    /// <summary>已发布（工作室内所有人可见）。</summary>
    Published = 2,

    /// <summary>已驳回（上传者可修改后重提）。</summary>
    Rejected = 3,
}
