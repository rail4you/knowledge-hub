using KnowledgeHub;
using KnowledgeHub.ResourceLibraries;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace KnowledgeHub.EntityFrameworkCore.ResourceLibraries;

public static class ResourceLibraryDbModelCreatingExtensions
{
    public static void ConfigureResourceLibrary(this ModelBuilder builder)
    {
        builder.Entity<ResourceLibrary>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "ResourceLibraries", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Name).IsRequired().HasMaxLength(ResourceLibrary.MaxNameLength);
            b.Property(x => x.Description).HasMaxLength(ResourceLibrary.MaxDescriptionLength);
            b.Property(x => x.CoverUrl).HasMaxLength(ResourceLibrary.MaxCoverUrlLength);
            b.Property(x => x.Principal).HasMaxLength(ResourceLibrary.MaxPrincipalLength);
            b.Property(x => x.ConfigSnapshotJson).HasColumnType("text");

            // 「对外开放自学」（其他学员经手机号验证 + 实名认证后可学习）：校级私有库默认关闭
            b.HasIndex(x => x.OpenToExternalLearners);

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.OwnerTenantId);
            b.HasIndex(x => x.PrimaryMajorId);
            b.HasIndex(x => x.Status);
            // 可见性：院校管理员列表按「可见性 + 归属学校」过滤，走索引
            b.HasIndex(x => x.Visibility);

            b.HasMany(x => x.Members)
                .WithOne()
                .HasForeignKey(x => x.LibraryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ResourceLibraryMember>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "ResourceLibraryMembers", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.MemberTenantName).HasMaxLength(256);

            b.HasIndex(x => x.LibraryId);
            b.HasIndex(x => x.MemberTenantId);
            // 同一学校在同一库中只能有一条成员记录
            b.HasIndex(x => new { x.LibraryId, x.MemberTenantId }).IsUnique();
        });

        builder.Entity<ResourceLibraryTeacher>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "ResourceLibraryTeachers", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.UserName).HasMaxLength(256);

            b.HasIndex(x => x.LibraryId);
            b.HasIndex(x => x.UserId);
            b.HasIndex(x => x.TenantId);
            // 同一教师在同一库中只能有一条分配记录（一个老师可在多个私有库 → 多条不同 LibraryId 的记录）
            b.HasIndex(x => new { x.LibraryId, x.UserId }).IsUnique();
        });

        // 库内内容准入 / 发布记录（V2.0）
        builder.Entity<ResourcePublication>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "ResourcePublications", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.PolicyJson).HasMaxLength(ResourcePublication.MaxPolicyJsonLength);
            b.Property(x => x.RejectReason).HasMaxLength(ResourcePublication.MaxRejectReasonLength);

            b.HasIndex(x => new { x.LibraryId, x.ContentType, x.ContentId }).IsUnique();
            b.HasIndex(x => new { x.LibraryId, x.Status });
            b.HasIndex(x => x.ContentTenantId);
            b.HasIndex(x => x.ReviewTicketId);
        });


        // ── V2.0 模板 + 共建任务 ──
        builder.Entity<ResourceLibraryTemplate>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "ResourceLibraryTemplates", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Code).IsRequired().HasMaxLength(ResourceLibraryTemplate.MaxCodeLength);
            b.Property(x => x.Name).IsRequired().HasMaxLength(ResourceLibraryTemplate.MaxNameLength);
            b.Property(x => x.ConfigJson).HasColumnType("text");
            b.HasIndex(x => new { x.Code, x.Version }).IsUnique();
            b.HasIndex(x => x.Status);
        });

        builder.Entity<BuildTaskTemplate>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "BuildTaskTemplates", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Code).IsRequired().HasMaxLength(BuildTaskTemplate.MaxCodeLength);
            b.Property(x => x.Name).IsRequired().HasMaxLength(BuildTaskTemplate.MaxNameLength);
            b.HasIndex(x => x.LibraryId);
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BuildTaskTemplateItem>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "BuildTaskTemplateItems", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ItemName).IsRequired().HasMaxLength(BuildTaskTemplateItem.MaxItemNameLength);
            b.Property(x => x.Acceptance).HasMaxLength(BuildTaskTemplateItem.MaxAcceptanceLength);
            b.HasIndex(x => x.TemplateId);
        });

        builder.Entity<BuildTask>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "BuildTasks", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Title).HasMaxLength(300).IsRequired();
            b.Property(x => x.Requirements).HasMaxLength(BuildTask.MaxRequirementsLength);
            b.HasIndex(x => new { x.LibraryId, x.Status });
            b.HasIndex(x => x.OwnerSchoolId);
            b.HasIndex(x => x.OwnerUserId);
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Collaborators).WithOne().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BuildTaskItem>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "BuildTaskItems", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ItemName).IsRequired().HasMaxLength(BuildTaskTemplateItem.MaxItemNameLength);
            b.Property(x => x.Acceptance).HasMaxLength(BuildTaskTemplateItem.MaxAcceptanceLength);
            b.HasIndex(x => x.TaskId);
        });

        builder.Entity<BuildTaskCollaborator>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "BuildTaskCollaborators", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();
            b.HasIndex(x => x.TaskId);
            b.HasIndex(x => new { x.SchoolId, x.UserId });
        });

        builder.Entity<BuildTaskDelivery>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "BuildTaskDeliveries", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Comment).HasMaxLength(BuildTaskDelivery.MaxCommentLength);
            b.HasIndex(x => new { x.TaskId, x.Status });
            b.HasIndex(x => x.TaskItemId);
            b.HasIndex(x => x.SubmittedBy);
        });

        builder.Entity<LibraryRoleAssignment>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "LibraryRoleAssignments", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();
            b.HasIndex(x => new { x.LibraryId, x.UserId }).IsUnique();
            b.HasIndex(x => x.TenantId);
        });


        // ── V2.0 跨校引用 + 使用事件 ──
        builder.Entity<CourseSharedRef>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "CourseSharedRefs", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.RevokedReason).HasMaxLength(CourseSharedRef.MaxRevokedReasonLength);
            b.HasIndex(x => new { x.TargetTenantId, x.CourseId, x.Status });
            b.HasIndex(x => new { x.ContentId, x.TargetTenantId });
            b.HasIndex(x => x.LibraryId);
            b.HasIndex(x => x.SourceTenantId);
        });

        builder.Entity<ResourceUsageEvent>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "ResourceUsageEvents", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Type).IsRequired().HasMaxLength(ResourceUsageEvent.MaxTypeLength);
            b.HasIndex(x => x.EventId).IsUnique();
            b.HasIndex(x => new { x.SchoolId, x.ResourceId, x.OccurredAt });
            b.HasIndex(x => x.RefId);
        });

        // 课程 ↔ 资源库 多对多
        builder.Entity<ResourceLibraryCourse>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "ResourceLibraryCourses", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.HasIndex(x => x.LibraryId);
            b.HasIndex(x => x.CourseId);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.LibraryId, x.CourseId }).IsUnique();
        });

        // 实训项目 ↔ 资源库 多对多
        builder.Entity<ResourceLibraryPracticum>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "ResourceLibraryPracticums", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.HasIndex(x => x.LibraryId);
            b.HasIndex(x => x.PracticumProjectId);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.LibraryId, x.PracticumProjectId }).IsUnique();
        });

        // 「其他学员」档案（PRD 5.3）：与班级成员天然隔离，归主建老师账号下
        builder.Entity<ResourceLibraryLearner>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "ResourceLibraryLearners", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.DisplayName).IsRequired().HasMaxLength(ResourceLibraryLearner.MaxDisplayNameLength);
            b.Property(x => x.Phone).HasMaxLength(ResourceLibraryLearner.MaxPhoneLength);
            b.Property(x => x.IdCardMasked).HasMaxLength(ResourceLibraryLearner.MaxIdCardLength);
            b.Property(x => x.RealName).HasMaxLength(ResourceLibraryLearner.MaxDisplayNameLength);
            b.Property(x => x.OwnerTeacherName).HasMaxLength(128);

            b.HasIndex(x => x.LibraryId);
            b.HasIndex(x => x.UserId);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.OwnerTeacherId);
            // 同一学员在同一库中只有一条档案
            b.HasIndex(x => new { x.LibraryId, x.UserId }).IsUnique();
        });

        // 资源 ↔ 资源库 多对多：一个资源可关联多个库，也可未关联（未分类）
        builder.Entity<ResourceLibraryResource>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "ResourceLibraryResources", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.HasIndex(x => x.LibraryId);
            b.HasIndex(x => x.ResourceId);
            b.HasIndex(x => x.TenantId);
            // 同一资源在同一库中只能有一条关联记录
            b.HasIndex(x => new { x.LibraryId, x.ResourceId }).IsUnique();
        });
    }
}
