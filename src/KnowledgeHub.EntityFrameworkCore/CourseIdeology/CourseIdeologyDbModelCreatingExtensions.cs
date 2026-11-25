using KnowledgeHub;
using KnowledgeHub.CourseIdeology;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace KnowledgeHub.EntityFrameworkCore.CourseIdeology;

/// <summary>
/// 课程思政模块表结构（5 张表，全部属于本模块；不改任何核心表）。
/// </summary>
public static class CourseIdeologyDbModelCreatingExtensions
{
    public static void ConfigureCourseIdeology(this ModelBuilder builder)
    {
        builder.Entity<IdeologyElement>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "IdeologyElements", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Name).IsRequired().HasMaxLength(IdeologyElement.MaxNameLength);
            b.Property(x => x.Category).HasMaxLength(64);
            b.Property(x => x.Description).HasMaxLength(500);

            // 租户内元素名唯一（同租户不能有两个「家国情怀」）。
            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.SortOrder });
        });

        builder.Entity<IdeologyCase>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "IdeologyCases", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Title).IsRequired().HasMaxLength(IdeologyCase.MaxTitleLength);
            b.Property(x => x.Summary).HasMaxLength(IdeologyCase.MaxSummaryLength);
            b.Property(x => x.ContentJson).HasColumnType("text");
            b.Property(x => x.SourceText).IsRequired().HasMaxLength(IdeologyCase.MaxSourceTextLength);
            b.Property(x => x.SourceUrl).HasMaxLength(IdeologyCase.MaxSourceUrlLength);
            b.Property(x => x.ChapterTitle).HasMaxLength(256);
            b.Property(x => x.AiMaterial).HasMaxLength(4000);
            b.Property(x => x.CreatorName).HasMaxLength(128);

            b.HasIndex(x => new { x.CourseId, x.Status });
            b.HasIndex(x => new { x.CourseId, x.ChapterId });
            b.HasIndex(x => x.TenantId);

            b.HasMany(x => x.Elements)
                .WithOne()
                .HasForeignKey(x => x.CaseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<IdeologyCaseElement>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "IdeologyCaseElements", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            // 同一案例不重复挂同一元素。
            b.HasIndex(x => new { x.CaseId, x.ElementId }).IsUnique();
            b.HasIndex(x => x.ElementId);
        });

        builder.Entity<CourseIdeologySetting>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "CourseIdeologySettings", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.IdeologyGoal).HasMaxLength(2000);
            b.Property(x => x.Note).HasMaxLength(2000);

            // 一门课只有一行设置。
            b.HasIndex(x => new { x.TenantId, x.CourseId }).IsUnique();
        });

        builder.Entity<IdeologyLessonPlanInset>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "IdeologyLessonPlanInsets", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Title).HasMaxLength(200);
            b.Property(x => x.Content).HasMaxLength(2000);

            b.HasIndex(x => x.LessonPlanId);
            b.HasIndex(x => new { x.CourseId, x.CaseId });
        });
    }
}
