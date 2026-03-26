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
        public const string ViewStatistics = Default + ".ViewStatistics";
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
    }

    public static class Courses
    {
        public const string Default = GroupName + ".Courses";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Enroll = Default + ".Enroll";
    }

    public static class AI
    {
        public const string Default = GroupName + ".AI";
        public const string Chat = Default + ".Chat";
        public const string LessonPlan = Default + ".LessonPlan";
        public const string CaseAnalysis = Default + ".CaseAnalysis";
        public const string CareerGuidance = Default + ".CareerGuidance";
    }
}
