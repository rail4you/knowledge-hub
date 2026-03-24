using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace KnowledgeHub.EntityFrameworkCore.AI;

public static class AIDbModelCreatingExtensions
{
    public static void ConfigureAI(this ModelBuilder builder)
    {
        builder.Entity<KnowledgeHub.AI.ChatThread>(b =>
        {
            b.ToTable("ChatThreads");
            b.ConfigureByConvention();
            
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.UserId).IsRequired();
            b.Property(x => x.Title).HasMaxLength(500);
            b.Property(x => x.SessionData).HasMaxLength(-1);
        });

        builder.Entity<KnowledgeHub.AI.ChatMessage>(b =>
        {
            b.ToTable("ChatMessages");
            b.ConfigureByConvention();
            
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.ThreadId).IsRequired();
            b.Property(x => x.Role).HasMaxLength(50).IsRequired();
            b.Property(x => x.Content).IsRequired();
            b.Property(x => x.ToolCalls).HasMaxLength(-1);
            
            b.HasOne(x => x.Thread)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.ThreadId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
