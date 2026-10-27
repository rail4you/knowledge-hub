namespace KnowledgeHub.ResourceLibraries;

/// <summary>「其他学员」档案状态。</summary>
public enum ResourceLibraryLearnerStatus : byte
{
    /// <summary>正常（可学习、可申请证书）。</summary>
    Active = 0,

    /// <summary>已禁用（平台管理员停用）。</summary>
    Disabled = 1
}
