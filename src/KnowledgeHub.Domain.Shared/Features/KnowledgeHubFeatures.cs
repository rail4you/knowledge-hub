namespace KnowledgeHub.Features;

public static class KnowledgeHubFeatures
{
    public const string GroupName = "KnowledgeHub";
    private const string DefaultPrefix = GroupName + ".";

    public const string MaxTenantCount = DefaultPrefix + "MaxTenantCount";

    public const string Alliance = DefaultPrefix + "Alliance";

    public const string TwoLevelApproval = DefaultPrefix + "TwoLevelApproval";

    public const string SpecialEducation = DefaultPrefix + "SpecialEducation";

    public const string VoiceAssistant = DefaultPrefix + "VoiceAssistant";

    // ── V2.0 教学资源库主线（默认全部关闭，按租户灰度开通） ──

    /// <summary>资源库审核流：建库 / 改库 / 删库 / 内容入库走两级（或单级）审核。</summary>
    public const string LibraryReview = DefaultPrefix + "LibraryReview";

    /// <summary>共建任务：库模板、任务模板、任务分派与交付验收。</summary>
    public const string BuildTask = DefaultPrefix + "BuildTask";

    /// <summary>跨校引用：把共建库资源引用进本校课程（含版本固定与撤销）。</summary>
    public const string CrossSchoolRef = DefaultPrefix + "CrossSchoolRef";

    /// <summary>产教合作企业工作台：企业档案、合作需求、项目台账。</summary>
    public const string EnterpriseWorkspace = DefaultPrefix + "EnterpriseWorkspace";

    /// <summary>单位有效期门禁：学校 / 企业到期后禁止写操作（读保持放行）。</summary>
    public const string OrganizationValidity = DefaultPrefix + "OrganizationValidity";
}