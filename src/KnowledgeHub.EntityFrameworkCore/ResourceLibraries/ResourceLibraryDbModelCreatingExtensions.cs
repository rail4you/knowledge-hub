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
