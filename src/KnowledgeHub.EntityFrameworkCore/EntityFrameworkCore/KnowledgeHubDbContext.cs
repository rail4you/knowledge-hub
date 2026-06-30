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
using KnowledgeHub.EntityFrameworkCore.Majors;
using KnowledgeHub.EntityFrameworkCore.TenantInfos;
using KnowledgeHub.Exams;
using KnowledgeHub.Alliance;
using KnowledgeHub.News;
using KnowledgeHub.Majors;
using KnowledgeHub.TenantInfos;
using KnowledgeHub.MicroMajors;
using KnowledgeHub.Practicums;
using KnowledgeHub.DoubleHigh;
using KnowledgeHub.Employment;
using KnowledgeHub.TeachingAgents;
using KnowledgeHub.EntityFrameworkCore.TeachingAgents;
using KnowledgeHub.KnowledgeGraph;
using KnowledgeHub.RecruitmentLive;
using KnowledgeHub.EntityFrameworkCore.RecruitmentLive;

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
    public DbSet<ResourcePageIndex> ResourcePageIndices { get; set; }

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
    public DbSet<KnowledgeHub.Learning.StudentExerciseRecord> StudentExerciseRecords { get; set; }

    // Exam entities
    public DbSet<Exam> Exams { get; set; }
    public DbSet<Exercise> Exercises { get; set; }
    public DbSet<ChapterExercise> ChapterExercises { get; set; }
    public DbSet<ExamExercise> ExamExercises { get; set; }
    public DbSet<StudentExam> StudentExams { get; set; }
    public DbSet<StudentAnswer> StudentAnswers { get; set; }

    // Alliance entities
    public DbSet<KnowledgeHub.Alliance.Alliance> Alliances { get; set; }
    public DbSet<KnowledgeHub.Alliance.AllianceMember> AllianceMembers { get; set; }
    public DbSet<KnowledgeHub.Alliance.AllianceAudit> AllianceAudits { get; set; }

    // News entities
    public DbSet<NewsCategory> NewsCategories { get; set; }
    public DbSet<NewsArticle> NewsArticles { get; set; }
    public DbSet<NewsAudit> NewsAudits { get; set; }
    public DbSet<NewsComment> NewsComments { get; set; }
    public DbSet<NewsReaction> NewsReactions { get; set; }

    // KnowledgeGraph entities
    public DbSet<KnowledgeNode> KnowledgeNodes { get; set; }
    public DbSet<KnowledgeRelation> KnowledgeRelations { get; set; }

    // Micro major entities
    public DbSet<MicroMajor> MicroMajors { get; set; }
    public DbSet<MicroMajorCourse> MicroMajorCourses { get; set; }
    public DbSet<MicroMajorEnrollment> MicroMajorEnrollments { get; set; }
    public DbSet<MicroMajorCertificate> MicroMajorCertificates { get; set; }
    public DbSet<MicroMajorResource> MicroMajorResources { get; set; }

    // Major entities
    public DbSet<Major> Majors { get; set; }

    // TenantInfo entities
    public DbSet<TenantInfo> TenantInfos { get; set; }

    // Practicum entities
    public DbSet<PracticumProject> PracticumProjects { get; set; }
    public DbSet<PracticumTask> PracticumTasks { get; set; }
    public DbSet<PracticumMaterial> PracticumMaterials { get; set; }
    public DbSet<PracticumEnrollment> PracticumEnrollments { get; set; }
    public DbSet<PracticumSubmission> PracticumSubmissions { get; set; }
    public DbSet<PracticumGuidanceRecord> PracticumGuidanceRecords { get; set; }
    public DbSet<PracticumAssessment> PracticumAssessments { get; set; }
    public DbSet<PracticumChatMessage> PracticumChatMessages { get; set; }

    // Double high entities
    public DbSet<DoubleHighProject> DoubleHighProjects { get; set; }
    public DbSet<DoubleHighIndicator> DoubleHighIndicators { get; set; }
    public DbSet<DoubleHighIndicatorValue> DoubleHighIndicatorValues { get; set; }
    public DbSet<DoubleHighEvidence> DoubleHighEvidences { get; set; }
    public DbSet<DoubleHighReport> DoubleHighReports { get; set; }

    // Employment entities
    public DbSet<JobPosting> JobPostings { get; set; }
    public DbSet<StudentResume> StudentResumes { get; set; }
    public DbSet<JobApplication> JobApplications { get; set; }
    public DbSet<InterviewSchedule> InterviewSchedules { get; set; }
    public DbSet<EmploymentGuidanceRecord> EmploymentGuidanceRecords { get; set; }
    public DbSet<EmploymentOutcome> EmploymentOutcomes { get; set; }
    
    // Teaching agent entities
    public DbSet<TeachingAgent> TeachingAgents { get; set; }
    public DbSet<TeachingAgentVersion> TeachingAgentVersions { get; set; }
    public DbSet<ClassroomAgentTask> ClassroomAgentTasks { get; set; }
    public DbSet<ClassroomAgentAssignment> ClassroomAgentAssignments { get; set; }
    public DbSet<AgentRun> AgentRuns { get; set; }
    public DbSet<AgentRunMessage> AgentRunMessages { get; set; }

    // RecruitmentLive entities
    public DbSet<global::KnowledgeHub.RecruitmentLive.RecruitmentLive> RecruitmentLives { get; set; }

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
        builder.ConfigureKnowledgeGraph();
        builder.ConfigureExam();
        builder.ConfigureAlliance();
        builder.ConfigureNews();
        builder.ConfigureMicroMajor();
        builder.ConfigureMajor();
        builder.ConfigurePracticum();
        builder.ConfigureDoubleHigh();
        builder.ConfigureEmployment();
        builder.ConfigureRecruitmentLive();
        builder.ConfigureTeachingAgents();
        builder.ConfigureTenantInfo();
    }
}
