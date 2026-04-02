using Microsoft.EntityFrameworkCore;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.BlobStoring.Database.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.TenantManagement;
using Volo.Abp.TenantManagement.EntityFrameworkCore;
using KnowledgeHub.Resources;
using KnowledgeHub.Domain.Search;
using KnowledgeHub.EntityFrameworkCore.AI;
using KnowledgeHub.Exams;
using KnowledgeHub.Alliance;

namespace KnowledgeHub.EntityFrameworkCore;

[ReplaceDbContext(typeof(IIdentityDbContext))]
[ReplaceDbContext(typeof(ITenantManagementDbContext))]
[ConnectionStringName("Default")]
public class KnowledgeHubDbContext :
    AbpDbContext<KnowledgeHubDbContext>,
    ITenantManagementDbContext,
    IIdentityDbContext
{
    /* Add DbSet properties for your Aggregate Roots / Entities here. */


    #region Entities from the modules

    /* Notice: We only implemented IIdentityProDbContext and ISaasDbContext
     * and replaced them for this DbContext. This allows you to perform JOIN
     * queries for the entities of these modules over the repositories easily. You
     * typically don't need that for other modules. But, if you need, you can
     * implement the DbContext interface of the needed module and use ReplaceDbContext
     * attribute just like IIdentityProDbContext and ISaasDbContext.
     *
     * More info: Replacing a DbContext of a module ensures that the related module
     * uses this DbContext on runtime. Otherwise, it will use its own DbContext class.
     */

    // Identity
    public DbSet<IdentityUser> Users { get; set; }
    public DbSet<IdentityRole> Roles { get; set; }
    public DbSet<IdentityClaimType> ClaimTypes { get; set; }
    public DbSet<OrganizationUnit> OrganizationUnits { get; set; }
    public DbSet<IdentitySecurityLog> SecurityLogs { get; set; }
    public DbSet<IdentityLinkUser> LinkUsers { get; set; }
    public DbSet<IdentityUserDelegation> UserDelegations { get; set; }
    public DbSet<IdentitySession> Sessions { get; set; }

    // Tenant Management
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<TenantConnectionString> TenantConnectionStrings { get; set; }

    #endregion
    public DbSet<Resource> Resources { get; set; }
    public DbSet<ResourceVersion> ResourceVersions { get; set; }
    public DbSet<ResourceCategory> ResourceCategories { get; set; }
    public DbSet<ResourceAudit> ResourceAudits { get; set; }
    public DbSet<ResourceCollection> ResourceCollections { get; set; }
    public DbSet<PhysicalDeleteRequest> PhysicalDeleteRequests { get; set; }

    // Search entities
    public DbSet<DocumentIndex> DocumentIndices { get; set; }
    public DbSet<SearchQuery> SearchQueries { get; set; }
    public DbSet<ResourceViewLog> ResourceViewLogs { get; set; }
    public DbSet<SearchStatistics> SearchStatistics { get; set; }
    public DbSet<ResourceExposure> ResourceExposures { get; set; }
    public DbSet<DocumentIndexingJob> DocumentIndexingJobs { get; set; }
    public DbSet<VideoIndexingJob> VideoIndexingJobs { get; set; }
    public DbSet<PageContent> PageContents { get; set; }
    public DbSet<ResourceReview> ResourceReviews { get; set; }

    // AI Chat entities
    public DbSet<KnowledgeHub.AI.ChatThread> ChatThreads { get; set; }
    public DbSet<KnowledgeHub.AI.ChatMessage> ChatMessages { get; set; }

    // Course entities
    public DbSet<KnowledgeHub.Courses.Course> Courses { get; set; }
    public DbSet<KnowledgeHub.Courses.Chapter> Chapters { get; set; }
    public DbSet<KnowledgeHub.Courses.KnowledgeResource> KnowledgeResources { get; set; }

    // Learning entities
    public DbSet<KnowledgeHub.Learning.StudentCourse> StudentCourses { get; set; }
    public DbSet<KnowledgeHub.Learning.LearningProgress> LearningProgresses { get; set; }
    public DbSet<KnowledgeHub.Learning.KnowledgeMastery> KnowledgeMasteries { get; set; }

    // Exam entities
    public DbSet<Exam> Exams { get; set; }
    public DbSet<Exercise> Exercises { get; set; }
    public DbSet<ExamExercise> ExamExercises { get; set; }
    public DbSet<StudentExam> StudentExams { get; set; }
    public DbSet<StudentAnswer> StudentAnswers { get; set; }

    // Alliance entities
    public DbSet<KnowledgeHub.Alliance.Alliance> Alliances { get; set; }
    public DbSet<KnowledgeHub.Alliance.AllianceMember> AllianceMembers { get; set; }
    public DbSet<KnowledgeHub.Alliance.AllianceAudit> AllianceAudits { get; set; }

    public KnowledgeHubDbContext(DbContextOptions<KnowledgeHubDbContext> options)
        : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        /* Include modules to your migration db context */

        builder.ConfigurePermissionManagement();
        builder.ConfigureSettingManagement();
        builder.ConfigureBackgroundJobs();
        builder.ConfigureAuditLogging();
        builder.ConfigureFeatureManagement();
        builder.ConfigureIdentity();
        builder.ConfigureOpenIddict();
        builder.ConfigureTenantManagement();
        builder.ConfigureBlobStoring();
        
        /* Configure your own tables/entities inside here */
        builder.ConfigureResource();
        builder.ConfigureSearch();
        builder.ConfigureAI();
        builder.ConfigureCourse();
        builder.ConfigureLearning();
        builder.ConfigureExam();
        builder.ConfigureAlliance();
    }
}
