using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Users;
using Microsoft.Extensions.DependencyInjection;

namespace KnowledgeHub;

public class GrantAllPoliciesContributor : IApplicationConfigurationContributor, ITransientDependency
{
    private readonly IPermissionDefinitionManager _permissionManager;

    public GrantAllPoliciesContributor(IPermissionDefinitionManager permissionManager)
    {
        _permissionManager = permissionManager;
    }

    public async Task ContributeAsync(ApplicationConfigurationContributorContext context)
    {
        var currentUser = context.ServiceProvider.GetRequiredService<ICurrentUser>();
        if (!currentUser.IsAuthenticated) return;

        var policies = context.ApplicationConfiguration.Auth.GrantedPolicies;
        var permissions = await _permissionManager.GetPermissionsAsync();

        foreach (var perm in permissions)
        {
            GrantRecursive(perm, policies);
        }
    }

    private static void GrantRecursive(PermissionDefinition perm, System.Collections.Generic.Dictionary<string, bool> policies)
    {
        if (!policies.ContainsKey(perm.Name))
        {
            policies[perm.Name] = true;
        }
        foreach (var child in perm.Children)
        {
            GrantRecursive(child, policies);
        }
    }
}
