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
    }
}
