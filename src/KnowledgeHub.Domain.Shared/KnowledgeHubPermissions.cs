namespace KnowledgeHub.Permissions;

public static class KnowledgeHubPermissions
{
    public const string GroupName = "KnowledgeHub";

    public static class Documents
    {
        public const string Default = GroupName + ".Documents";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }
    
    public static class Resources
    {
        public const string Default = GroupName + ".Resources";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Download = Default + ".Download";
        public const string SchoolAudit = Default + ".SchoolAudit";
        public const string LeagueAudit = Default + ".LeagueAudit";
        public const string ManageCategory = Default + ".ManageCategory";
        public const string PhysicalDelete = Default + ".PhysicalDelete";
        public const string RequestDelete = Default + ".RequestDelete";
        public const string ViewStatistics = Default + ".ViewStatistics";
        public const string ViewRecommendation = Default + ".ViewRecommendation";
        public const string Share = Default + ".Share";
        public const string ManageShare = Default + ".ManageShare";
    }

    public static class Users
    {
        public const string Default = GroupName + ".Users";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Import = Default + ".Import";
    }

    public static class Search
    {
        public const string Default = GroupName + ".Search";
        public const string ManageIndex = Default + ".ManageIndex";
        public const string ViewStatistics = Default + ".ViewStatistics";
        public const string ReviewResource = Default + ".ReviewResource";
    }

    public static class Courses
    {
        public const string Default = GroupName + ".Courses";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Enroll = Default + ".Enroll";
        public const string ManageEnrollment = Default + ".ManageEnrollment";
    }

    public static class AI
    {
        public const string Default = GroupName + ".AI";
        public const string Chat = Default + ".Chat";
        public const string LessonPlan = Default + ".LessonPlan";
        public const string CaseAnalysis = Default + ".CaseAnalysis";
        public const string CareerGuidance = Default + ".CareerGuidance";
        public const string ExerciseGenerate = Default + ".ExerciseGenerate";
        /// <summary>教学图片生成（通义万相文生图）。</summary>
        public const string ImageGeneration = Default + ".ImageGeneration";
        /// <summary>教学短视频生成（通义万相图生视频）。</summary>
        public const string VideoGeneration = Default + ".VideoGeneration";
        /// <summary>可查看/管理本租户所有人的 AI 生成任务（任务监控面板）。</summary>
        public const string ManageTasks = Default + ".ManageTasks";
    }

    public static class TeachingAgents
    {
        public const string Default = GroupName + ".TeachingAgents";
        public const string Manage = Default + ".Manage";
        public const string Assign = Default + ".Assign";
        public const string Execute = Default + ".Execute";
        public const string Review = Default + ".Review";
    }

    public static class Alliance
    {
        public const string Default = GroupName + ".Alliance";
        public const string Create = Default + ".Create";
        public const string Update = Default + ".Update";
        public const string Delete = Default + ".Delete";
        public const string ManageMembers = Default + ".ManageMembers";
    }

    public static class Learning
    {
        public const string Default = GroupName + ".Learning";
        public const string ViewStatistics = Default + ".ViewStatistics";
        public const string ExportData = Default + ".ExportData";
    }

    public static class News
    {
        public const string Default = GroupName + ".News";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Review = Default + ".Review";
        public const string Publish = Default + ".Publish";
        public const string ManageComment = Default + ".ManageComment";
    }

    public static class MicroMajors
    {
        public const string Default = GroupName + ".MicroMajors";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string ManageEnrollment = Default + ".ManageEnrollment";
        public const string IssueCertificate = Default + ".IssueCertificate";
        public const string ViewStatistics = Default + ".ViewStatistics";
    }

    public static class Majors
    {
        public const string Default = GroupName + ".Majors";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class Practicum
    {
        public const string Default = GroupName + ".Practicum";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Review = Default + ".Review";
        public const string Score = Default + ".Score";
        public const string Export = Default + ".Export";
        public const string ViewStatistics = Default + ".ViewStatistics";
    }

    public static class DoubleHigh
    {
        public const string Default = GroupName + ".DoubleHigh";
        public const string ManageProject = Default + ".ManageProject";
        public const string ManageIndicator = Default + ".ManageIndicator";
        public const string CollectData = Default + ".CollectData";
        public const string ExportReport = Default + ".ExportReport";
        public const string ViewAll = Default + ".ViewAll";
    }

    public static class Employment
    {
        public const string Default = GroupName + ".Employment";
        public const string PublishJob = Default + ".PublishJob";
        public const string ReviewJob = Default + ".ReviewJob";
        public const string ManageResume = Default + ".ManageResume";
        public const string ScheduleInterview = Default + ".ScheduleInterview";
        public const string ManageGuidance = Default + ".ManageGuidance";
        public const string ManageOutcome = Default + ".ManageOutcome";
        public const string ViewStatistics = Default + ".ViewStatistics";
        public const string ExportReport = Default + ".ExportReport";
        public const string ManageApplication = Default + ".ManageApplication";
        public const string ViewMyApplication = Default + ".ViewMyApplication";
    }

    public static class RecruitmentLive
    {
        public const string Default = GroupName + ".RecruitmentLive";
        public const string Create = Default + ".Create";
        public const string Manage = Default + ".Manage";
    }

    public static class TenantInfo
    {
        public const string Default = GroupName + ".TenantInfo";
        public const string Edit = Default + ".Edit";
    }

    /// <summary>
    /// 站点品牌设置（标题/副标题/页脚/Logo）：仅 host「admin」全局管理员可被授予。
    /// 与 AccountValidity 同模式：不加入 GrantAllPoliciesMiddleware 注入列表，
    /// 后端 Update 额外校验宿主上下文，防止租户管理员越权修改全局品牌。
    /// </summary>
    public static class Branding
    {
        public const string Default = GroupName + ".Branding";
    }

    /// <summary>
    /// SMTP 邮件设置（租户级）：租户 SchoolAdmin 配置本租户发信 SMTP，host admin 写全局回退配置。
    /// 与 Branding 不同：两侧都可授予，但各写各的（租户写租户级，host 写全局），互不影响。
    /// 不加入 GrantAllPoliciesMiddleware 注入列表，依赖 RolePermissionSeeder 数据库真实授权。
    /// </summary>
    public static class Smtp
    {
        public const string Default = GroupName + ".Smtp";
    }

    /// <summary>
    /// 阿里云 OSS 对象存储设置（AK/SK 属敏感基础设施凭证）：
    /// 租户 SchoolAdmin 可配本租户 Bucket/AK（写租户级 Setting，只影响本租户上传），
    /// host admin 写全局回退配置。与 Smtp 相同模式：两侧都可授予，但各写各的。
    /// </summary>
    public static class Oss
    {
        public const string Default = GroupName + ".Oss";
    }

    /// <summary>
    /// 存储容量监控（每租户文件容量与使用情况）：仅 host「admin」全局管理员可被授予。
    /// 统计跳租户数据（需要看所有租户），所以权限只开 host 侧，
    /// 不加入 GrantAllPoliciesMiddleware 注入列表，依赖 RolePermissionSeeder 数据库真实授权。
    /// </summary>
    public static class StorageMonitor
    {
        public const string Default = GroupName + ".StorageMonitor";
    }

    /// <summary>
    /// 账号有效期（多校协同）：仅 host「admin」全局管理员可被授予。
    /// 用于多校协同菜单 / 账号有效期配置页的可见性，以及 AccountValidityAppService 的后端鉴权。
    /// 注意：不要复用 KnowledgeHub.Users —— 它是租户用户管理权限（TenantUserAppService 依赖），
    /// 租户级 SchoolAdmin 也持有；复用会导致租户管理员看到"多校协同"菜单。
    /// </summary>
    public static class AccountValidity
    {
        public const string Default = GroupName + ".AccountValidity";
    }

    /// <summary>
    /// 特殊教育扩展模块：整体式权限，插件式开关。
    /// L1 Feature 按租户开通，L2 本组权限在租户内按角色分配。
    /// </summary>
    public static class SpecialEducation
    {
        public const string Default = GroupName + ".SpecialEducation";
        public const string TeachingDesign = Default + ".TeachingDesign";
        public const string IEP = Default + ".IEP";
        public const string Resource = Default + ".Resource";
        public const string Review = Default + ".Review";
        public const string Manage = Default + ".Manage";
    }

    /// <summary>
    /// 学生端语音助手：管理页按租户开/关 Feature，学生端按特性值显隐。
    /// </summary>
    public static class VoiceAssistant
    {
        public const string Default = GroupName + ".VoiceAssistant";
        public const string Manage = Default + ".Manage";
    }

    /// <summary>
    /// 学校档案（徽标 / 简介 / 联系方式）：
    /// <list type="bullet">
    /// <item><description><see cref="Default"/>：查看本租户学校档案（管理端配置页）。</description></item>
    /// <item><description><see cref="Edit"/>：修改本租户学校档案。</description></item>
    /// <item><description>
/// <see cref="ManagePublicSchool"/>：配置平台唯一「社会学校」租户 —— 仅 host「admin」全局管理员可被授予，
/// 决定公开注册用户归属与「共享到社会学校」的目标租户。
/// </description></item>
    /// </list>
    /// </summary>
    public static class Schools
    {
        public const string Default = GroupName + ".Schools";
        public const string Edit = Default + ".Edit";
        public const string ManagePublicSchool = Default + ".ManagePublicSchool";
    }

    /// <summary>
    /// 资源库（多校共建）：库由主建学校拥有，合作学校通过成员表加入。
    /// </summary>
    public static class ResourceLibraries
    {
        public const string Default = GroupName + ".ResourceLibraries";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string ManageMembers = Default + ".ManageMembers";
    }
}
