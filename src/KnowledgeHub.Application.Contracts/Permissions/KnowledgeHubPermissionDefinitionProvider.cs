using KnowledgeHub.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace KnowledgeHub.Permissions;

public class KnowledgeHubPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(KnowledgeHubPermissions.GroupName);

        //Define your own permissions here. Example:
        //myGroup.AddPermission(KnowledgeHubPermissions.MyPermission1, L("Permission:MyPermission1"));

        //Documents permissions
        var documentsPermission = myGroup.AddPermission(KnowledgeHubPermissions.Documents.Default, L("Permission:Documents"));
        documentsPermission.AddChild(KnowledgeHubPermissions.Documents.Create, L("Permission:Documents.Create"));
        documentsPermission.AddChild(KnowledgeHubPermissions.Documents.Edit, L("Permission:Documents.Edit"));
        documentsPermission.AddChild(KnowledgeHubPermissions.Documents.Delete, L("Permission:Documents.Delete"));

        //Resources permissions
        var resourcesPermission = myGroup.AddPermission(KnowledgeHubPermissions.Resources.Default, L("Permission:Resources"));
        resourcesPermission.AddChild(KnowledgeHubPermissions.Resources.Create, L("Permission:Resources.Create"));
        resourcesPermission.AddChild(KnowledgeHubPermissions.Resources.Edit, L("Permission:Resources.Edit"));
        resourcesPermission.AddChild(KnowledgeHubPermissions.Resources.Delete, L("Permission:Resources.Delete"));
        resourcesPermission.AddChild(KnowledgeHubPermissions.Resources.Download, L("Permission:Resources.Download"));
        resourcesPermission.AddChild(KnowledgeHubPermissions.Resources.SchoolAudit, L("Permission:Resources.SchoolAudit"));
        resourcesPermission.AddChild(KnowledgeHubPermissions.Resources.LeagueAudit, L("Permission:Resources.LeagueAudit"));
        resourcesPermission.AddChild(KnowledgeHubPermissions.Resources.ManageCategory, L("Permission:Resources.ManageCategory"));
        resourcesPermission.AddChild(KnowledgeHubPermissions.Resources.RequestDelete, L("Permission:Resources.RequestDelete"));
        resourcesPermission.AddChild(KnowledgeHubPermissions.Resources.PhysicalDelete, L("Permission:Resources.PhysicalDelete"));
        resourcesPermission.AddChild(KnowledgeHubPermissions.Resources.ViewStatistics, L("Permission:Resources.ViewStatistics"));
        resourcesPermission.AddChild(KnowledgeHubPermissions.Resources.ViewRecommendation, L("Permission:Resources.ViewRecommendation"));
        
        //Users permissions
        var usersPermission = myGroup.AddPermission(
            KnowledgeHubPermissions.Users.Default, L("Permission:Users"));
        usersPermission.AddChild(
            KnowledgeHubPermissions.Users.Create, L("Permission:Users.Create"));
        usersPermission.AddChild(
            KnowledgeHubPermissions.Users.Edit, L("Permission:Users.Edit"));
        usersPermission.AddChild(
            KnowledgeHubPermissions.Users.Delete, L("Permission:Users.Delete"));
        usersPermission.AddChild(
            KnowledgeHubPermissions.Users.Import, L("Permission:Users.Import"));

        //Search permissions
        var searchPermission = myGroup.AddPermission(KnowledgeHubPermissions.Search.Default, L("Permission:Search"));
        searchPermission.AddChild(KnowledgeHubPermissions.Search.ManageIndex, L("Permission:Search.ManageIndex"));
        searchPermission.AddChild(KnowledgeHubPermissions.Search.ViewStatistics, L("Permission:Search.ViewStatistics"));
        searchPermission.AddChild(KnowledgeHubPermissions.Search.ReviewResource, L("Permission:Search.ReviewResource"));

        //Courses permissions
        var coursesPermission = myGroup.AddPermission(KnowledgeHubPermissions.Courses.Default, L("Permission:Courses"));
        coursesPermission.AddChild(KnowledgeHubPermissions.Courses.Create, L("Permission:Courses.Create"));
        coursesPermission.AddChild(KnowledgeHubPermissions.Courses.Edit, L("Permission:Courses.Edit"));
        coursesPermission.AddChild(KnowledgeHubPermissions.Courses.Delete, L("Permission:Courses.Delete"));
        coursesPermission.AddChild(KnowledgeHubPermissions.Courses.Enroll, L("Permission:Courses.Enroll"));
        coursesPermission.AddChild(KnowledgeHubPermissions.Courses.ManageEnrollment, L("Permission:Courses.ManageEnrollment"));

        //AI permissions
        var aiPermission = myGroup.AddPermission(KnowledgeHubPermissions.AI.Default, L("Permission:AI"));
        aiPermission.AddChild(KnowledgeHubPermissions.AI.Chat, L("Permission:AI.Chat"));
        aiPermission.AddChild(KnowledgeHubPermissions.AI.LessonPlan, L("Permission:AI.LessonPlan"));
        aiPermission.AddChild(KnowledgeHubPermissions.AI.CaseAnalysis, L("Permission:AI.CaseAnalysis"));
        aiPermission.AddChild(KnowledgeHubPermissions.AI.CareerGuidance, L("Permission:AI.CareerGuidance"));

        // Teaching agent permissions
        var teachingAgentsPermission = myGroup.AddPermission(KnowledgeHubPermissions.TeachingAgents.Default, L("Permission:TeachingAgents"));
        teachingAgentsPermission.AddChild(KnowledgeHubPermissions.TeachingAgents.Manage, L("Permission:TeachingAgents.Manage"));
        teachingAgentsPermission.AddChild(KnowledgeHubPermissions.TeachingAgents.Assign, L("Permission:TeachingAgents.Assign"));
        teachingAgentsPermission.AddChild(KnowledgeHubPermissions.TeachingAgents.Execute, L("Permission:TeachingAgents.Execute"));
        teachingAgentsPermission.AddChild(KnowledgeHubPermissions.TeachingAgents.Review, L("Permission:TeachingAgents.Review"));

        //Alliance permissions
        var alliancePermission = myGroup.AddPermission(KnowledgeHubPermissions.Alliance.Default, L("Permission:Alliance"));
        alliancePermission.AddChild(KnowledgeHubPermissions.Alliance.Create, L("Permission:Alliance.Create"));
        alliancePermission.AddChild(KnowledgeHubPermissions.Alliance.Update, L("Permission:Alliance.Update"));
        alliancePermission.AddChild(KnowledgeHubPermissions.Alliance.Delete, L("Permission:Alliance.Delete"));
        alliancePermission.AddChild(KnowledgeHubPermissions.Alliance.ManageMembers, L("Permission:Alliance.ManageMembers"));

        //Learning permissions
        var learningPermission = myGroup.AddPermission(KnowledgeHubPermissions.Learning.Default, L("Permission:Learning"));
        learningPermission.AddChild(KnowledgeHubPermissions.Learning.ViewStatistics, L("Permission:Learning.ViewStatistics"));
        learningPermission.AddChild(KnowledgeHubPermissions.Learning.ExportData, L("Permission:Learning.ExportData"));

        // News permissions
        var newsPermission = myGroup.AddPermission(KnowledgeHubPermissions.News.Default, L("Permission:News"));
        newsPermission.AddChild(KnowledgeHubPermissions.News.Create, L("Permission:News.Create"));
        newsPermission.AddChild(KnowledgeHubPermissions.News.Edit, L("Permission:News.Edit"));
        newsPermission.AddChild(KnowledgeHubPermissions.News.Delete, L("Permission:News.Delete"));
        newsPermission.AddChild(KnowledgeHubPermissions.News.Review, L("Permission:News.Review"));
        newsPermission.AddChild(KnowledgeHubPermissions.News.Publish, L("Permission:News.Publish"));
        newsPermission.AddChild(KnowledgeHubPermissions.News.ManageComment, L("Permission:News.ManageComment"));

        // Micro major permissions
        var microMajorPermission = myGroup.AddPermission(KnowledgeHubPermissions.MicroMajors.Default, L("Permission:MicroMajors"));
        microMajorPermission.AddChild(KnowledgeHubPermissions.MicroMajors.Create, L("Permission:MicroMajors.Create"));
        microMajorPermission.AddChild(KnowledgeHubPermissions.MicroMajors.Edit, L("Permission:MicroMajors.Edit"));
        microMajorPermission.AddChild(KnowledgeHubPermissions.MicroMajors.Delete, L("Permission:MicroMajors.Delete"));
        microMajorPermission.AddChild(KnowledgeHubPermissions.MicroMajors.ManageEnrollment, L("Permission:MicroMajors.ManageEnrollment"));
        microMajorPermission.AddChild(KnowledgeHubPermissions.MicroMajors.IssueCertificate, L("Permission:MicroMajors.IssueCertificate"));
        microMajorPermission.AddChild(KnowledgeHubPermissions.MicroMajors.ViewStatistics, L("Permission:MicroMajors.ViewStatistics"));

        // Major permissions
        var majorPermission = myGroup.AddPermission(KnowledgeHubPermissions.Majors.Default, L("Permission:Majors"));
        majorPermission.AddChild(KnowledgeHubPermissions.Majors.Create, L("Permission:Majors.Create"));
        majorPermission.AddChild(KnowledgeHubPermissions.Majors.Edit, L("Permission:Majors.Edit"));
        majorPermission.AddChild(KnowledgeHubPermissions.Majors.Delete, L("Permission:Majors.Delete"));

        // Practicum permissions
        var practicumPermission = myGroup.AddPermission(KnowledgeHubPermissions.Practicum.Default, L("Permission:Practicum"));
        practicumPermission.AddChild(KnowledgeHubPermissions.Practicum.Create, L("Permission:Practicum.Create"));
        practicumPermission.AddChild(KnowledgeHubPermissions.Practicum.Edit, L("Permission:Practicum.Edit"));
        practicumPermission.AddChild(KnowledgeHubPermissions.Practicum.Review, L("Permission:Practicum.Review"));
        practicumPermission.AddChild(KnowledgeHubPermissions.Practicum.Score, L("Permission:Practicum.Score"));
        practicumPermission.AddChild(KnowledgeHubPermissions.Practicum.Export, L("Permission:Practicum.Export"));
        practicumPermission.AddChild(KnowledgeHubPermissions.Practicum.ViewStatistics, L("Permission:Practicum.ViewStatistics"));

        // Double high permissions
        var doubleHighPermission = myGroup.AddPermission(KnowledgeHubPermissions.DoubleHigh.Default, L("Permission:DoubleHigh"));
        doubleHighPermission.AddChild(KnowledgeHubPermissions.DoubleHigh.ManageProject, L("Permission:DoubleHigh.ManageProject"));
        doubleHighPermission.AddChild(KnowledgeHubPermissions.DoubleHigh.ManageIndicator, L("Permission:DoubleHigh.ManageIndicator"));
        doubleHighPermission.AddChild(KnowledgeHubPermissions.DoubleHigh.CollectData, L("Permission:DoubleHigh.CollectData"));
        doubleHighPermission.AddChild(KnowledgeHubPermissions.DoubleHigh.ExportReport, L("Permission:DoubleHigh.ExportReport"));
        doubleHighPermission.AddChild(KnowledgeHubPermissions.DoubleHigh.ViewAll, L("Permission:DoubleHigh.ViewAll"));

        // Employment permissions
        var employmentPermission = myGroup.AddPermission(KnowledgeHubPermissions.Employment.Default, L("Permission:Employment"));
        employmentPermission.AddChild(KnowledgeHubPermissions.Employment.PublishJob, L("Permission:Employment.PublishJob"));
        employmentPermission.AddChild(KnowledgeHubPermissions.Employment.ReviewJob, L("Permission:Employment.ReviewJob"));
        employmentPermission.AddChild(KnowledgeHubPermissions.Employment.ManageResume, L("Permission:Employment.ManageResume"));
        employmentPermission.AddChild(KnowledgeHubPermissions.Employment.ScheduleInterview, L("Permission:Employment.ScheduleInterview"));
        employmentPermission.AddChild(KnowledgeHubPermissions.Employment.ManageGuidance, L("Permission:Employment.ManageGuidance"));
        employmentPermission.AddChild(KnowledgeHubPermissions.Employment.ManageOutcome, L("Permission:Employment.ManageOutcome"));
        employmentPermission.AddChild(KnowledgeHubPermissions.Employment.ViewStatistics, L("Permission:Employment.ViewStatistics"));
        employmentPermission.AddChild(KnowledgeHubPermissions.Employment.ExportReport, L("Permission:Employment.ExportReport"));
        employmentPermission.AddChild(KnowledgeHubPermissions.Employment.ManageApplication, L("Permission:Employment.ManageApplication"));
        employmentPermission.AddChild(KnowledgeHubPermissions.Employment.ViewMyApplication, L("Permission:Employment.ViewMyApplication"));

        // RecruitmentLive permissions
        var recruitmentLivePermission = myGroup.AddPermission(KnowledgeHubPermissions.RecruitmentLive.Default, L("Permission:RecruitmentLive"));
        recruitmentLivePermission.AddChild(KnowledgeHubPermissions.RecruitmentLive.Create, L("Permission:RecruitmentLive.Create"));
        recruitmentLivePermission.AddChild(KnowledgeHubPermissions.RecruitmentLive.Manage, L("Permission:RecruitmentLive.Manage"));

        // TenantInfo permissions
        var tenantInfoPermission = myGroup.AddPermission(KnowledgeHubPermissions.TenantInfo.Default, L("Permission:TenantInfo"));
        tenantInfoPermission.AddChild(KnowledgeHubPermissions.TenantInfo.Edit, L("Permission:TenantInfo.Edit"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<KnowledgeHubResource>(name);
    }
}
