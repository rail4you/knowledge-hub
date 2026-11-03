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
using KnowledgeHub.EntityFrameworkCore.Accounts;
using KnowledgeHub.Accounts;
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
using KnowledgeHub.RecruitmentLive;
using KnowledgeHub.EntityFrameworkCore.RecruitmentLive;
using KnowledgeHub.EntityFrameworkCore.SpecialEducation;
using KnowledgeHub.EntityFrameworkCore.Schools;
using KnowledgeHub.EntityFrameworkCore.ResourceLibraries;
using KnowledgeHub.EntityFrameworkCore.Certificates;
using KnowledgeHub.EntityFrameworkCore.Review;
using KnowledgeHub.EntityFrameworkCore.Enterprises;
using KnowledgeHub.EntityFrameworkCore.Cooperation;
using KnowledgeHub.Cooperation;
using KnowledgeHub.Enterprises;
using KnowledgeHub.Review;
using KnowledgeHub.Schools;
using KnowledgeHub.ResourceLibraries;
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
    public DbSet<KnowledgeHub.Resources.ResourceMajor> ResourceMajors { get; set; }

    // 资源媒体处理（缩略图/预览 ETL）
    public DbSet<KnowledgeHub.Resources.Media.ResourceMediaJob> ResourceMediaJobs { get; set; }
    public DbSet<KnowledgeHub.Resources.Media.ResourceArtifact> ResourceArtifacts { get; set; }

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

    // AI 异步生成任务
    public DbSet<KnowledgeHub.AI.AiGenerationTask> AiGenerationTasks { get; set; }

    // AI 服务调用记录（用量审计 + 费用估算 + 配额统计）
    public DbSet<KnowledgeHub.AI.AiUsageRecord> AiUsageRecords { get; set; }

    // 教学场景模板（图片 / 视频）
    public DbSet<KnowledgeHub.AI.TeachingScene> TeachingScenes { get; set; }

    // Course entities
    public DbSet<KnowledgeHub.Courses.Course> Courses { get; set; }
    public DbSet<KnowledgeHub.Courses.Chapter> Chapters { get; set; }
    public DbSet<KnowledgeHub.Courses.KnowledgeResource> KnowledgeResources { get; set; }
    public DbSet<KnowledgeHub.Courses.ChapterResource> ChapterResources { get; set; }
    public DbSet<KnowledgeHub.Courses.CourseResource> CourseResources { get; set; }
    public DbSet<KnowledgeHub.Courses.CourseLessonPlan> CourseLessonPlans { get; set; }
    public DbSet<KnowledgeHub.Courses.CourseCase> CourseCases { get; set; }
    public DbSet<KnowledgeHub.Courses.CourseMajor> CourseMajors { get; set; }

    // 教学班（班级是教学组织单元：建班、成员维护、按班级批量选课）
    // 注意：ABP 的默认仓储注册依赖 DbSet 属性发现实体，
    // 只在 ConfigureCourse() 里 builder.Entity<T>() 是不够的 —— 会导致
    // IRepository<TeachingClass, Guid> 无法解析（DI 报错）。
    public DbSet<KnowledgeHub.Courses.TeachingClass> TeachingClasses { get; set; }
    public DbSet<KnowledgeHub.Courses.TeachingClassMember> TeachingClassMembers { get; set; }

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
    public DbSet<NewsCommentLike> NewsCommentLikes { get; set; }
    public DbSet<NewsReaction> NewsReactions { get; set; }

    // KnowledgeGraph entities
    public DbSet<KnowledgeNode> KnowledgeNodes { get; set; }
    public DbSet<KnowledgeRelation> KnowledgeRelations { get; set; }

    // Micro major entities
    public DbSet<MicroMajor> MicroMajors { get; set; }
    public DbSet<MicroMajorCourse> MicroMajorCourses { get; set; }
    public DbSet<MicroMajorEnrollment> MicroMajorEnrollments { get; set; }
    public DbSet<MicroMajorCertificate> MicroMajorCertificates { get; set; }
    public DbSet<MicroMajorCertificateTemplate> MicroMajorCertificateTemplates { get; set; }
    public DbSet<MicroMajorResource> MicroMajorResources { get; set; }

    // Major entities
    public DbSet<Major> Majors { get; set; }

    // TenantInfo entities
    public DbSet<TenantInfo> TenantInfos { get; set; }

    // 学校实体（与租户 1:1 的机构档案：徽标 / 简介 / 联系方式）
    public DbSet<School> Schools { get; set; }

    // 教学资源库实体（归属学校拥有 + 共建学校成员表 + 教师分配表）
    public DbSet<ResourceLibrary> ResourceLibraries { get; set; }
    public DbSet<ResourceLibraryMember> ResourceLibraryMembers { get; set; }
    public DbSet<ResourceLibraryTeacher> ResourceLibraryTeachers { get; set; }
    /// <summary>资源 ↔ 资源库多对多关联（一个资源可关联多个库）。</summary>
    public DbSet<ResourceLibraryResource> ResourceLibraryResources { get; set; }
    /// <summary>课程 ↔ 资源库多对多关联。</summary>
    public DbSet<ResourceLibraryCourse> ResourceLibraryCourses { get; set; }
    /// <summary>实训项目 ↔ 资源库多对多关联。</summary>
    public DbSet<ResourceLibraryPracticum> ResourceLibraryPracticums { get; set; }
    /// <summary>「其他学员」档案（PRD 5.3）。</summary>
    public DbSet<ResourceLibraryLearner> ResourceLibraryLearners { get; set; }

    /// <summary>库内内容准入 / 发布记录（V2.0 审核）。</summary>
    public DbSet<ResourcePublication> ResourcePublications { get; set; }

    // V2.0 模板 + 共建任务
    public DbSet<ResourceLibraryTemplate> ResourceLibraryTemplates { get; set; }
    public DbSet<BuildTaskTemplate> BuildTaskTemplates { get; set; }
    public DbSet<BuildTaskTemplateItem> BuildTaskTemplateItems { get; set; }
    public DbSet<BuildTask> BuildTasks { get; set; }
    public DbSet<BuildTaskItem> BuildTaskItems { get; set; }
    public DbSet<BuildTaskCollaborator> BuildTaskCollaborators { get; set; }
    public DbSet<BuildTaskDelivery> BuildTaskDeliveries { get; set; }
    public DbSet<LibraryRoleAssignment> LibraryRoleAssignments { get; set; }

    // V2.0 企业主体 + 成员（产教合作）
    public DbSet<EnterpriseProfile> EnterpriseProfiles { get; set; }
    public DbSet<EnterpriseMember> EnterpriseMembers { get; set; }

    // V2.0 产教合作：需求 / 响应 / 项目 / 里程碑
    public DbSet<CooperationDemand> CooperationDemands { get; set; }
    public DbSet<CooperationResponse> CooperationResponses { get; set; }
    public DbSet<CooperationProject> CooperationProjects { get; set; }
    public DbSet<ProjectMilestone> ProjectMilestones { get; set; }

    // V2.0 跨校引用 + 使用事件
    public DbSet<CourseSharedRef> CourseSharedRefs { get; set; }
    public DbSet<ResourceUsageEvent> ResourceUsageEvents { get; set; }

    // 审核内核（V2.0）：统一审核单 + 只追加的审核明细
    public DbSet<ReviewTicket> ReviewTickets { get; set; }
    public DbSet<ReviewAction> ReviewActions { get; set; }

    // 结业证书实体（平台模板 / 学员申请 / 已发放证书）
    public DbSet<KnowledgeHub.Certificates.CertificateTemplate> CertificateTemplates { get; set; }
    public DbSet<KnowledgeHub.Certificates.CertificateApplication> CertificateApplications { get; set; }
    public DbSet<KnowledgeHub.Certificates.Certificate> Certificates { get; set; }

    // 课程共享实体（课程共享到其它学校 / 社会学校）
    public DbSet<KnowledgeHub.Courses.CourseShare> CourseShares { get; set; }

    // Practicum entities
    public DbSet<PracticumProject> PracticumProjects { get; set; }
    public DbSet<PracticumTask> PracticumTasks { get; set; }
    public DbSet<PracticumMaterial> PracticumMaterials { get; set; }
    public DbSet<PracticumSimulation> PracticumSimulations { get; set; }
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
    public DbSet<DoubleHighProjectDocument> DoubleHighProjectDocuments { get; set; }
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
    public DbSet<RecruitmentLiveChatMessage> RecruitmentLiveChatMessages { get; set; }
public DbSet<RecruitmentLiveParticipant> RecruitmentLiveParticipants { get; set; }

    // Account validity (multi-school account lifecycle) entities
    public DbSet<AccountValidity> AccountValidities { get; set; }

    // Special education extension module entities
    public DbSet<KnowledgeHub.SpecialEducation.SpecialTeachingDesign> SpecialTeachingDesigns { get; set; }
    public DbSet<KnowledgeHub.SpecialEducation.IepPlan> IepPlans { get; set; }
    public DbSet<KnowledgeHub.SpecialEducation.SpecialEduResource> SpecialEduResources { get; set; }
    public DbSet<KnowledgeHub.SpecialEducation.SpecialEduContentVersion> SpecialEduContentVersions { get; set; }

    // 智学分析（LearningAnalytics）实体
    public DbSet<KnowledgeHub.LearningAnalytics.KnowledgePoint> KnowledgePoints { get; set; }
    public DbSet<KnowledgeHub.LearningAnalytics.ExerciseKnowledgePoint> ExerciseKnowledgePoints { get; set; }
    public DbSet<KnowledgeHub.LearningAnalytics.AnswerRecord> AnswerRecords { get; set; }
    public DbSet<KnowledgeHub.LearningAnalytics.StudentKpMastery> StudentKpMasteries { get; set; }
    public DbSet<KnowledgeHub.LearningAnalytics.KpPrerequisite> KpPrerequisites { get; set; }
    public DbSet<KnowledgeHub.LearningAnalytics.CourseLink> CourseLinks { get; set; }
    public DbSet<KnowledgeHub.LearningAnalytics.MasteryThreshold> MasteryThresholds { get; set; }
    public DbSet<KnowledgeHub.LearningAnalytics.PaperBatch> PaperBatches { get; set; }
    public DbSet<KnowledgeHub.LearningAnalytics.PaperUploadImage> PaperUploadImages { get; set; }
    public DbSet<KnowledgeHub.LearningAnalytics.PaperScript> PaperScripts { get; set; }
    public DbSet<KnowledgeHub.LearningAnalytics.PaperItemScore> PaperItemScores { get; set; }
    public DbSet<KnowledgeHub.LearningAnalytics.AnalysisReport> AnalysisReports { get; set; }
    public DbSet<KnowledgeHub.LearningAnalytics.AnalysisReportFile> AnalysisReportFiles { get; set; }
    public DbSet<KnowledgeHub.LearningAnalytics.StudentNotification> StudentNotifications { get; set; }
    public DbSet<KnowledgeHub.LearningAnalytics.ReviewPackage> ReviewPackages { get; set; }
    public DbSet<KnowledgeHub.LearningAnalytics.ReviewPackageItem> ReviewPackageItems { get; set; }
    public DbSet<KnowledgeHub.LearningAnalytics.TeachingInsight> TeachingInsights { get; set; }

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
        builder.ConfigureSchool();
        builder.ConfigureResourceLibrary();
        builder.ConfigureReview();
        builder.ConfigureEnterprises();
        builder.ConfigureCooperation();
        builder.ConfigureCertificates();
        builder.ConfigureAccountValidity();
        builder.ConfigureSpecialEdu();
        builder.ConfigureLearningAnalytics();
    }
}
