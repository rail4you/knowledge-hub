namespace KnowledgeHub.ResourceLibraries;

/// <summary>共建任务状态（V2.0）。</summary>
public enum BuildTaskStatus : byte
{
    /// <summary>草稿：仅创建人可见，可继续编辑交付项。</summary>
    Draft = 0,

    /// <summary>已发布：协作校与教师可见，可提交交付物。</summary>
    Published = 1,

    /// <summary>进行中：已收到至少一项交付，尚未全部验收。</summary>
    InProgress = 2,

    /// <summary>已提交：全部交付项均已有提交，等待验收。</summary>
    Submitted = 3,

    /// <summary>已验收：全部交付项验收通过（≠ 资源发布，资源仍需走准入审核）。</summary>
    Accepted = 4,

    /// <summary>已退回：存在被退回的交付项，需补交。</summary>
    Returned = 5,

    /// <summary>已关闭：不再接收交付（归档 / 取消）。</summary>
    Closed = 6
}

/// <summary>任务协作能力。</summary>
public enum BuildTaskCapability : byte
{
    /// <summary>可提交交付物。</summary>
    Contribute = 0,

    /// <summary>只读查看。</summary>
    View = 1,

    /// <summary>主责校（可验收）。</summary>
    Owner = 2
}

/// <summary>交付物状态。</summary>
public enum BuildTaskDeliveryStatus : byte
{
    /// <summary>已提交，待验收。</summary>
    Submitted = 0,

    /// <summary>验收通过（计入进度）。</summary>
    Accepted = 1,

    /// <summary>已退回（不计入进度，需补交）。</summary>
    Returned = 2
}

/// <summary>资源库模板状态。</summary>
public enum LibraryTemplateStatus : byte
{
    /// <summary>草稿：不可用于建库。</summary>
    Draft = 0,

    /// <summary>已发布：可用于建库（版本号唯一）。</summary>
    Published = 1,

    /// <summary>已停用：存量库不受影响，新库不可再选。</summary>
    Disabled = 2
}

/// <summary>库级角色（替代新增系统角色，见设计方案 D2/C4）。</summary>
public enum LibraryRole : byte
{
    /// <summary>库管理员：建任务、分派、验收（限本库）。</summary>
    Manager = 0,

    /// <summary>共建者：可提交交付物（等同现有教师分配的"可建设"语义）。</summary>
    Contributor = 1,

    /// <summary>审核人：被指派的校审人（一期仅登记，不强制启用）。</summary>
    Auditor = 2
}
