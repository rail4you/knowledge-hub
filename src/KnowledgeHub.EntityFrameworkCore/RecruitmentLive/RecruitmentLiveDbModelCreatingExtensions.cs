using KnowledgeHub.RecruitmentLive;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace KnowledgeHub.EntityFrameworkCore.RecruitmentLive;

public static class RecruitmentLiveDbModelCreatingExtensions
{
    public static void ConfigureRecruitmentLive(this ModelBuilder builder)
    {
        builder.Entity<global::KnowledgeHub.RecruitmentLive.RecruitmentLive>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "RecruitmentLives", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Title).IsRequired().HasMaxLength(200);
            b.Property(x => x.Description).HasMaxLength(1000);
            b.Property(x => x.TeacherName).IsRequired().HasMaxLength(100);
            b.Property(x => x.StudentName).HasMaxLength(100);
            b.Property(x => x.RoomCode).IsRequired().HasMaxLength(10);

            b.HasIndex(x => x.RoomCode).IsUnique();
            b.HasIndex(x => x.TeacherId);
            b.HasIndex(x => x.StudentId);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.TenantId);
        });
    }
}
