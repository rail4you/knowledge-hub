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
    }
}
