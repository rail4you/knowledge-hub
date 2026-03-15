using KnowledgeHub.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;

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
        
        //Users permissions
        var usersPermission = myGroup.AddPermission(
            KnowledgeHubPermissions.Users.Default, L("Permission:Users"));
        usersPermission.AddChild(
            KnowledgeHubPermissions.Users.Create, L("Permission:Users.Create"));
        usersPermission.AddChild(
            KnowledgeHubPermissions.Users.Edit, L("Permission:Users.Edit"));
        usersPermission.AddChild(
            KnowledgeHubPermissions.Users.Delete, L("Permission:Users.Delete"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<KnowledgeHubResource>(name);
    }
}
