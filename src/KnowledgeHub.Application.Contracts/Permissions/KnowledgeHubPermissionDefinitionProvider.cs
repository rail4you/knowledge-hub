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
        resourcesPermission.AddChild(KnowledgeHubPermissions.Resources.PhysicalDelete, L("Permission:Resources.PhysicalDelete"));
        resourcesPermission.AddChild(KnowledgeHubPermissions.Resources.ViewStatistics, L("Permission:Resources.ViewStatistics"));
        
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

        //Courses permissions
        var coursesPermission = myGroup.AddPermission(KnowledgeHubPermissions.Courses.Default, L("Permission:Courses"));
        coursesPermission.AddChild(KnowledgeHubPermissions.Courses.Create, L("Permission:Courses.Create"));
        coursesPermission.AddChild(KnowledgeHubPermissions.Courses.Edit, L("Permission:Courses.Edit"));
        coursesPermission.AddChild(KnowledgeHubPermissions.Courses.Delete, L("Permission:Courses.Delete"));
        coursesPermission.AddChild(KnowledgeHubPermissions.Courses.Enroll, L("Permission:Courses.Enroll"));

        //AI permissions
        var aiPermission = myGroup.AddPermission(KnowledgeHubPermissions.AI.Default, L("Permission:AI"));
        aiPermission.AddChild(KnowledgeHubPermissions.AI.Chat, L("Permission:AI.Chat"));
        aiPermission.AddChild(KnowledgeHubPermissions.AI.LessonPlan, L("Permission:AI.LessonPlan"));
        aiPermission.AddChild(KnowledgeHubPermissions.AI.CaseAnalysis, L("Permission:AI.CaseAnalysis"));
        aiPermission.AddChild(KnowledgeHubPermissions.AI.CareerGuidance, L("Permission:AI.CareerGuidance"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<KnowledgeHubResource>(name);
    }
}
