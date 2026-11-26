using KnowledgeHub;
using KnowledgeHub.Classroom;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace KnowledgeHub.EntityFrameworkCore.Classroom;

/// <summary>
/// 课堂互动（讨论答疑 / 签到 / 课堂练习 / 小组任务）表结构。
/// <para>
/// 通用约定：
/// ① 短链 token 列建**唯一索引**（并发创建同一 token 直接冲突而不是静默覆盖）；
/// ② 「一人一次」类约束（签到 / 练习提交 / 组提交 / 组成员）都用联合唯一索引兜底，
///    应用层先查再插只是优化，唯一索引才是最终保证；
/// ③ 所有表都带 TenantId（IMultiTenant），跨租户读取一律显式关过滤器。
/// </para>
/// </summary>
public static class ClassroomDbModelCreatingExtensions
{
    public static void ConfigureClassroom(this ModelBuilder builder)
    {
        // ── 讨论答疑 ──────────────────────────────────────────────
        builder.Entity<Discussion>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "Discussions", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Title).IsRequired().HasMaxLength(256);
            b.Property(x => x.Content).IsRequired().HasMaxLength(8000);
            b.Property(x => x.AuthorName).HasMaxLength(128);

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.CourseId);
            b.HasIndex(x => x.ChapterId);
            b.HasIndex(x => x.KnowledgeResourceId);
            b.HasIndex(x => x.AuthorId);
            b.HasIndex(x => new { x.CourseId, x.IsResolved });
        });

        builder.Entity<DiscussionReply>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "DiscussionReplies", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Content).IsRequired().HasMaxLength(8000);
            b.Property(x => x.AuthorName).HasMaxLength(128);

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.DiscussionId);
            b.HasIndex(x => x.ParentReplyId);
        });

        // ── 签到 ─────────────────────────────────────────────────
        builder.Entity<CheckInSession>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "CheckInSessions", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Title).IsRequired().HasMaxLength(256);
            b.Property(x => x.Description).HasMaxLength(1000);
            b.Property(x => x.Token).IsRequired().HasMaxLength(64);

            b.HasIndex(x => x.Token).IsUnique();
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.CourseId);
            b.HasIndex(x => x.InitiatorId);
            b.HasIndex(x => new { x.CourseId, x.Status });
        });

        builder.Entity<CheckInRecord>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "CheckInRecords", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.StudentName).HasMaxLength(128);
            b.Property(x => x.Location).HasMaxLength(256);
            b.Property(x => x.Metadata).HasMaxLength(2000);

            // 一人一场次只能签一次：并发重复提交由唯一索引兜底
            b.HasIndex(x => new { x.SessionId, x.StudentId }).IsUnique();
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.StudentId);
        });

        // ── 课堂练习 ──────────────────────────────────────────────
        builder.Entity<PracticeSession>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "PracticeSessions", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Title).IsRequired().HasMaxLength(256);
            b.Property(x => x.Token).IsRequired().HasMaxLength(64);
            b.Property(x => x.KnowledgeResourceIds).HasMaxLength(4000);

            b.HasIndex(x => x.Token).IsUnique();
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.CourseId);
            b.HasIndex(x => x.InitiatorId);
            b.HasIndex(x => new { x.CourseId, x.Status });
        });

        builder.Entity<PracticeSessionItem>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "PracticeSessionItems", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.HasIndex(x => new { x.SessionId, x.ExerciseId }).IsUnique();
            b.HasIndex(x => x.TenantId);
        });

        builder.Entity<PracticeAttempt>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "PracticeAttempts", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.StudentName).HasMaxLength(128);

            b.HasIndex(x => new { x.SessionId, x.StudentId }).IsUnique();
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.StudentId);
        });

        builder.Entity<PracticeAttemptAnswer>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "PracticeAttemptAnswers", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Answer).HasMaxLength(4000);

            b.HasIndex(x => new { x.AttemptId, x.ExerciseId }).IsUnique();
            b.HasIndex(x => x.TenantId);
        });

        // ── 小组任务 ──────────────────────────────────────────────
        builder.Entity<GroupTask>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "GroupTasks", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Title).IsRequired().HasMaxLength(256);
            b.Property(x => x.Description).HasMaxLength(4000);
            b.Property(x => x.Token).IsRequired().HasMaxLength(64);

            b.HasIndex(x => x.Token).IsUnique();
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.CourseId);
            b.HasIndex(x => x.InitiatorId);
            b.HasIndex(x => new { x.CourseId, x.Status });
        });

        builder.Entity<StudentGroup>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "StudentGroups", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Name).IsRequired().HasMaxLength(128);

            b.HasIndex(x => x.GroupTaskId);
            b.HasIndex(x => x.TenantId);
        });

        builder.Entity<StudentGroupMember>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "StudentGroupMembers", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.StudentName).HasMaxLength(128);

            b.HasIndex(x => new { x.GroupId, x.StudentId }).IsUnique();
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.StudentId);
        });

        builder.Entity<GroupTaskSubmission>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "GroupTaskSubmissions", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Content).HasMaxLength(8000);
            b.Property(x => x.SubmittedByName).HasMaxLength(128);
            b.Property(x => x.Feedback).HasMaxLength(2000);

            // 一组一份：组内任一成员提交都覆盖同一行
            b.HasIndex(x => new { x.GroupTaskId, x.GroupId }).IsUnique();
            b.HasIndex(x => x.TenantId);
        });
    }
}
