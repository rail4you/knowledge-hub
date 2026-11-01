using KnowledgeHub.Review;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace KnowledgeHub.EntityFrameworkCore.Review;

/// <summary>
/// 审核内核（V2.0）的 EF 映射。
///
/// <para>表：<c>AppReviewTickets</c>（审核单）+ <c>AppReviewActions</c>（只追加的审核明细）。</para>
///
/// <para>
/// 索引要点：
/// </para>
/// <list type="bullet">
/// <item><description>待办查询：<c>(Status, Stage)</c> —— 校审看 School、联盟看 League。</description></item>
/// <item><description>目标回溯：<c>(TargetType, TargetId, ActionType, Status)</c>。</description></item>
/// <item><description>库维度：<c>(LibraryId, Status)</c>。</description></item>
/// <item><description>
/// 幂等：<c>(TargetType, TargetId, ActionType, PayloadHash)</c> 上的<b>部分唯一索引</b>
/// （仅 <c>Status = 0</c> 待审行），保证「同目标同内容」不会产生两条待审单，
/// 而历史（已通过/已驳回/已撤回）可以无限累积。
/// </description></item>
/// </list>
/// </summary>
public static class ReviewDbModelCreatingExtensions
{
    public static void ConfigureReview(this ModelBuilder builder)
    {
        builder.Entity<ReviewTicket>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "ReviewTickets", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            // 变更快照可能较大：用 text，避免 varchar(n) 截断导致「审通过的配置和提交的不一致」
            b.Property(x => x.PayloadJson).HasColumnType("text").IsRequired();
            b.Property(x => x.PayloadHash).HasMaxLength(ReviewTicket.MaxPayloadHashLength);
            b.Property(x => x.Reason).HasMaxLength(ReviewTicket.MaxReasonLength);
            b.Property(x => x.SubmittedByName).HasMaxLength(128);
            b.Property(x => x.ReviewerName).HasMaxLength(128);

            b.HasIndex(x => new { x.TargetType, x.TargetId, x.ActionType, x.Status });
            b.HasIndex(x => new { x.Status, x.Stage });
            b.HasIndex(x => new { x.LibraryId, x.Status });
            b.HasIndex(x => x.SubmittedBy);

            // 幂等：待审单在「目标 + 动作 + 内容哈希」上唯一。
            // PayloadHash 为 NULL 的行不受约束（PostgreSQL 中 NULL 互不相等），
            // 因此缺哈希的历史数据不会互相冲突。
            b.HasIndex(x => new { x.TargetType, x.TargetId, x.ActionType, x.PayloadHash })
                .IsUnique()
                .HasFilter($"\"Status\" = {(byte)ReviewTicketStatus.Pending}")
                .HasDatabaseName("IX_AppReviewTickets_Pending_Dedup");

            b.HasMany(x => x.Actions)
                .WithOne()
                .HasForeignKey(x => x.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ReviewAction>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "ReviewActions", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.ActorName).HasMaxLength(128);
            b.Property(x => x.Reason).HasMaxLength(ReviewTicket.MaxReasonLength);
            b.Property(x => x.SnapshotJson).HasColumnType("text");

            b.HasIndex(x => x.TicketId);
            b.HasIndex(x => new { x.TicketId, x.CreationTime });
        });
    }
}
