using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 教学资源库教师分配 —— 记录某个教师被院校管理员分配到哪个教学资源库。
///
/// <para>
/// 需求「院校管理员可以分配租户内的教师到不同的私有资源库，一个老师可以在多个私有资源库」：
/// 一个教师对同一个库最多一条分配记录；同一个教师在不同库有多条记录。
/// </para>
///
/// <para>
/// 分配记录同时用于 <b>可见性判定</b>：
/// 私有库只有被分配的教师能看到；公开库 / 共建库默认对参与方全体教师可见，
/// 分配记录在这些库上作为「显式加入」的补充。
/// </para>
///
/// <para>
/// <see cref="TenantId"/> 是<b>教师所属学校</b>（即分配发起方所在租户），
/// 不是库的归属租户 —— 共建库里可能存在关联学校教师被主学校看到的需求，
/// 因此该字段与 <c>ResourceLibrary.TenantId</c> 不等价。
/// </para>
/// </summary>
public class ResourceLibraryTeacher : Entity<Guid>, IMultiTenant
{
    /// <summary>教学资源库 Id。</summary>
    public Guid LibraryId { get; set; }

    /// <summary>教师用户 Id（ABP <c>IdentityUser</c> 的 Id）。</summary>
    public Guid UserId { get; set; }

    /// <summary>教师姓名快照（冗余，避免列表页 N+1 查询）。</summary>
    public string? UserName { get; set; }

    /// <summary>教师所属学校租户 Id（分配发起方）。</summary>
    public Guid? TenantId { get; set; }

    /// <summary>分配时间。</summary>
    public DateTime AssignedTime { get; set; }

    public ResourceLibraryTeacher()
    {
    }

    public ResourceLibraryTeacher(
        Guid id,
        Guid libraryId,
        Guid userId,
        string? userName,
        Guid? tenantId)
        : base(id)
    {
        LibraryId = libraryId;
        UserId = userId;
        UserName = userName;
        TenantId = tenantId;
        AssignedTime = DateTime.UtcNow;
    }
}
