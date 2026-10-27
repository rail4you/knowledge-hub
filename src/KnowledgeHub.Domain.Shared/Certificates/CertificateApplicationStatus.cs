namespace KnowledgeHub.Certificates;

/// <summary>结业证书申请状态。</summary>
public enum CertificateApplicationStatus : byte
{
    /// <summary>待审核（学员已提交申请，等待平台管理员审核）。</summary>
    Pending = 0,

    /// <summary>审核通过并已发证。</summary>
    Approved = 1,

    /// <summary>审核驳回。</summary>
    Rejected = 2,

    /// <summary>学员撤回。</summary>
    Cancelled = 3
}
