using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Permissions;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.UI.Navigation;

namespace KnowledgeHub.Web;

public class KnowledgeHubMenuContributor : IMenuContributor
{
    public Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        if (context.Menu.Name == StandardMenus.Main)
        {
            ConfigureMainMenu(context);
        }

        return Task.CompletedTask;
    }

    private void ConfigureMainMenu(MenuConfigurationContext context)
    {
        var administration = context.Menu.Items
            .FirstOrDefault(i => i.Name == "Abp.Application.Main.Administration");
        if (administration == null) return;
        
        administration.AddItem(
            new ApplicationMenuItem(
                "UserImport",
                "用户导入",
                "fa fa-upload",
                "/identity/users/import"
            ).RequirePermissions(KnowledgeHubPermissions.Users.Import)
        );

        administration.AddItem(
            new ApplicationMenuItem(
                "Resources",
                "资源管理",
                "fa fa-folder",
                "/resources"
            ).RequirePermissions(KnowledgeHubPermissions.Resources.Default)
        );

        administration.AddItem(
            new ApplicationMenuItem(
                "ResourceCategories",
                "资源分类",
                "fa fa-tags",
                "/resources/categories"
            ).RequirePermissions(KnowledgeHubPermissions.Resources.ManageCategory)
        );

        administration.AddItem(
            new ApplicationMenuItem(
                "ResourceAudit",
                "资源审核",
                "fa fa-check-circle",
                "/resources/audit"
            ).RequirePermissions(KnowledgeHubPermissions.Resources.SchoolAudit, KnowledgeHubPermissions.Resources.LeagueAudit)
        );

        administration.AddItem(
            new ApplicationMenuItem(
                "SearchManagement",
                "搜索管理",
                "fa fa-search",
                "/search"
            ).RequirePermissions(KnowledgeHubPermissions.Search.Default)
        );

        administration.AddItem(
            new ApplicationMenuItem(
                "TeachingAgents",
                "教学智能体",
                "fa fa-robot",
                "/teaching/agents"
            ).RequirePermissions(KnowledgeHubPermissions.TeachingAgents.Manage)
        );

        administration.AddItem(
            new ApplicationMenuItem(
                "TeachingAgentTasks",
                "课堂智能体任务",
                "fa fa-chalkboard",
                "/teaching/agent-tasks"
            ).RequirePermissions(KnowledgeHubPermissions.TeachingAgents.Assign)
        );
    }
}
