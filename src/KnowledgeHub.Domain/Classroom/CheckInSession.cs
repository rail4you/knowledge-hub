using System;
using KnowledgeHub.Classroom.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Classroom;

/// <summary>
/// 签到场次。教师发起 → 生成 token（二维码/短链）→ 学生在有效期内签到。
/// <para>
/// <see cref="Token"/> 不带租户信息：服务端按 token 反查出 <see cref="IMultiTenant.TenantId"/>
/// 再切租户上下文，因此 URL 可以很短（<c>/check-in/{token}</c>），
/// 也不会被浏览器残留的 <c>__tenant</c> cookie 污染。
/// </para>
/// </summary>
public class CheckInSession : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid CourseId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>短链 token（唯一，加密随机）。</summary>
    public string Token { get; set; } = string.Empty;

    public CheckInSessionStatus Status { get; set; } = CheckInSessionStatus.Active;

    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>结束时间（为空表示手动关闭）。</summary>
    public DateTime? EndTime { get; set; }

    /// <summary>发起人（教师）。注意不能叫 CreatorId —— 基类 FullAuditedAggregateRoot 已有 Guid? CreatorId，
    /// 同名 Guid 属性会让 EF 模型创建直接报错。</summary>
    public Guid InitiatorId { get; set; }

    /// <summary>是否要求定位（只记录经纬度，不做人脸核验）。</summary>
    public bool RequireLocation { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    /// <summary>允许签到半径（米）；与经纬度同时存在时生效。</summary>
    public int? RadiusMeters { get; set; }

    private CheckInSession() { }

    public CheckInSession(Guid id, Guid courseId, string title, string token, Guid creatorId) : base(id)
    {
        CourseId = courseId;
        Title = title;
        Token = token;
        InitiatorId = creatorId;
    }

    public void Close()
    {
        Status = CheckInSessionStatus.Closed;
        EndTime = DateTime.UtcNow;
    }

    /// <summary>是否还能签到（状态 + 时间窗）。</summary>
    public bool IsOpen(DateTime now) =>
        Status == CheckInSessionStatus.Active && (!EndTime.HasValue || EndTime.Value > now);
}
