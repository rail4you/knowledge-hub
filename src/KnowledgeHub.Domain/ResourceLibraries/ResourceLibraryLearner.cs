using System;
using KnowledgeHub.ResourceLibraries;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 教学资源库的「<b>其他学员</b>」档案 —— PRD 5.3「其他学员文件夹机制」。
///
/// <para>
/// PRD 规定：
/// </para>
/// <list type="bullet">
/// <item><description>
/// 「其他学员」是<b>系统级默认目录</b>，不需要老师手动创建；
/// </description></item>
/// <item><description>
/// 当有第一个其他学员开始学习该资源库时，系统在<b>主建老师账号</b>下自动创建该学员档案；
/// </description></item>
/// <item><description>
/// 该目录内学员<b>不归属任何班级</b>，与老师自建班级天然隔离，支持按学员维度查看、导出；
/// </description></item>
/// <item><description>
/// 只在<b>校级共享库</b>与<b>校际共建库</b>下存在（校级私有库不对外开放自学）。
/// </description></item>
/// </list>
///
/// <para>
/// 与 <see cref="TeachingClassMember"/> 的区别：班级成员是「老师自建班级里的在册学生」，
/// 本实体是「未经班级导入、自行注册学习的外部用户」，两者数据互不可见。
/// </para>
/// </summary>
public class ResourceLibraryLearner : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public const int MaxDisplayNameLength = 128;
    public const int MaxPhoneLength = 32;
    public const int MaxIdCardLength = 64;

    /// <summary>所属资源库 Id。</summary>
    public Guid LibraryId { get; set; }

    /// <summary>资源库归属租户（主建学校）—— 决定数据归属。</summary>
    public Guid? TenantId { get; set; }

    /// <summary>主建老师（建设负责人）用户 Id —— 「其他学员」目录归档到该账号下。</summary>
    public Guid? OwnerTeacherId { get; set; }

    /// <summary>主建老师姓名快照。</summary>
    public string? OwnerTeacherName { get; set; }

    /// <summary>学员用户 Id。</summary>
    public Guid UserId { get; set; }

    /// <summary>学员所属租户（外部学员通常是「社会学校」租户）。</summary>
    public Guid? LearnerTenantId { get; set; }

    /// <summary>学员姓名（实名认证后的真实姓名；未实名时为昵称）。</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>手机号（脱敏前原值仅本人与主建老师可见）。</summary>
    public string? Phone { get; set; }

    /// <summary>手机号是否已验证。</summary>
    public bool IsPhoneVerified { get; set; }

    /// <summary>手机号验证时间。</summary>
    public DateTime? PhoneVerifiedAt { get; set; }

    /// <summary>实名（真实姓名）。</summary>
    public string? RealName { get; set; }

    /// <summary>身份证号（脱敏存储）。</summary>
    public string? IdCardMasked { get; set; }

    /// <summary>是否已实名认证。</summary>
    public bool IsRealNameVerified { get; set; }

    /// <summary>实名认证时间。</summary>
    public DateTime? RealNameVerifiedAt { get; set; }

    /// <summary>首次学习时间（PRD：首次开始学习时自动建档）。</summary>
    public DateTime? FirstLearningAt { get; set; }

    /// <summary>建档时间。</summary>
    public DateTime EnrolledAt { get; set; }

    /// <summary>状态。</summary>
    public ResourceLibraryLearnerStatus Status { get; set; } = ResourceLibraryLearnerStatus.Active;

    /// <summary>
    /// 学员来源（PRD 5.1 数据归属矩阵）：
    /// 自学外部用户归入主建老师「其他学员」目录；老师自建班级导入的学员归入班级目录。
    /// </summary>
    public ResourceLibraryLearnerSource Source { get; set; } = ResourceLibraryLearnerSource.SelfLearner;

    public ResourceLibraryLearner()
    {
    }

    public ResourceLibraryLearner(
        Guid id,
        Guid libraryId,
        Guid userId,
        string displayName,
        Guid? ownerTenantId,
        Guid? ownerTeacherId = null,
        string? ownerTeacherName = null,
        Guid? learnerTenantId = null)
        : base(id)
    {
        LibraryId = libraryId;
        UserId = userId;
        DisplayName = displayName;
        TenantId = ownerTenantId;
        OwnerTeacherId = ownerTeacherId;
        OwnerTeacherName = ownerTeacherName;
        LearnerTenantId = learnerTenantId;
        EnrolledAt = DateTime.UtcNow;
    }

    /// <summary>记录首次学习时间（重复调用不覆盖）。</summary>
    public void MarkFirstLearning(DateTime time)
    {
        if (FirstLearningAt == null)
        {
            FirstLearningAt = time;
        }
    }

    /// <summary>设置手机号并标记已验证。</summary>
    public void VerifyPhone(string phone, DateTime time)
    {
        Phone = phone;
        IsPhoneVerified = true;
        PhoneVerifiedAt = time;
    }

    /// <summary>设置实名信息并标记已实名认证。</summary>
    public void VerifyRealName(string realName, string? idCardMasked, DateTime time)
    {
        RealName = realName;
        IdCardMasked = idCardMasked;
        IsRealNameVerified = true;
        RealNameVerifiedAt = time;
        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            DisplayName = realName;
        }
    }

    public void SetStatus(ResourceLibraryLearnerStatus status)
    {
        Status = status;
    }

    public void SetSource(ResourceLibraryLearnerSource source)
    {
        Source = source;
    }

    public void SetOwnerTeacher(Guid? ownerTeacherId, string? ownerTeacherName)
    {
        OwnerTeacherId = ownerTeacherId;
        OwnerTeacherName = ownerTeacherName;
    }
}
