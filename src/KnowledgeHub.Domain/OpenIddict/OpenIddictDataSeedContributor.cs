using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OpenIddict.Abstractions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.OpenIddict;
using Volo.Abp.OpenIddict.Applications;
using Volo.Abp.OpenIddict.Scopes;
using Volo.Abp.Uow;

namespace KnowledgeHub.OpenIddict;

/* Creates initial data that is needed to property run the application
 * and make client-to-server communication possible.
 */
public class OpenIddictDataSeedContributor : OpenIddictDataSeedContributorBase, IDataSeedContributor, ITransientDependency
{
    public OpenIddictDataSeedContributor(
        IConfiguration configuration,
        IOpenIddictApplicationRepository openIddictApplicationRepository,
        IAbpApplicationManager applicationManager,
        IOpenIddictScopeRepository openIddictScopeRepository,
        IOpenIddictScopeManager scopeManager)
        : base(configuration, openIddictApplicationRepository, applicationManager, openIddictScopeRepository, scopeManager)
    {
    }

    [UnitOfWork]
    public virtual async Task SeedAsync(DataSeedContext context)
    {
        await CreateScopesAsync();
        await CreateApplicationsAsync();
    }

    private async Task CreateScopesAsync()
    {
        await CreateScopesAsync(new OpenIddictScopeDescriptor 
        {
            Name = "KnowledgeHub", 
            DisplayName = "KnowledgeHub API", 
            Resources = { "KnowledgeHub" }
        });
    }

    private async Task CreateApplicationsAsync()
    {
        var commonScopes = new List<string> {
            OpenIddictConstants.Permissions.Scopes.Address,
            OpenIddictConstants.Permissions.Scopes.Email,
            OpenIddictConstants.Permissions.Scopes.Phone,
            OpenIddictConstants.Permissions.Scopes.Profile,
            OpenIddictConstants.Permissions.Scopes.Roles,
            "KnowledgeHub"
        };

        var configurationSection = Configuration.GetSection("OpenIddict:Applications");


        //Console Test / Angular Client
        var consoleAndAngularClientId = configurationSection["KnowledgeHub_App:ClientId"];
        if (!consoleAndAngularClientId.IsNullOrWhiteSpace())
        {
            var consoleAndAngularClientRootUrl = configurationSection["KnowledgeHub_App:RootUrl"]?.TrimEnd('/');
            var consoleAndAngularRedirectUrls = GetConfiguredUrls(
                configurationSection["KnowledgeHub_App:RedirectAllowedUrls"],
                consoleAndAngularClientRootUrl
            );
            await CreateOrUpdateApplicationAsync(
                applicationType: OpenIddictConstants.ApplicationTypes.Web,
                name: consoleAndAngularClientId!,
                type: OpenIddictConstants.ClientTypes.Public,
                consentType: OpenIddictConstants.ConsentTypes.Implicit,
                displayName: "Console Test / Angular Application",
                secret: null,
                grantTypes: new List<string> {
                    OpenIddictConstants.GrantTypes.AuthorizationCode,
                    OpenIddictConstants.GrantTypes.Password,
                    OpenIddictConstants.GrantTypes.ClientCredentials,
                    OpenIddictConstants.GrantTypes.RefreshToken,
                    "LinkLogin",
                    "Impersonation"
                },
                scopes: commonScopes,
                redirectUris: consoleAndAngularRedirectUrls,
                postLogoutRedirectUris: consoleAndAngularRedirectUrls,
                clientUri: consoleAndAngularClientRootUrl,
                logoUri: "/images/clients/angular.svg"
            );
        }

        var studentReactClientId = configurationSection["KnowledgeHub_StudentReact:ClientId"];
        if (!studentReactClientId.IsNullOrWhiteSpace())
        {
            var studentReactRootUrl = configurationSection["KnowledgeHub_StudentReact:RootUrl"]?.TrimEnd('/');
            await CreateOrUpdateApplicationAsync(
                applicationType: OpenIddictConstants.ApplicationTypes.Web,
                name: studentReactClientId!,
                type: OpenIddictConstants.ClientTypes.Public,
                consentType: OpenIddictConstants.ConsentTypes.Implicit,
                displayName: "Student React Application",
                secret: null,
                grantTypes: new List<string> {
                    OpenIddictConstants.GrantTypes.AuthorizationCode,
                    OpenIddictConstants.GrantTypes.Password,
                    OpenIddictConstants.GrantTypes.RefreshToken
                },
                scopes: commonScopes,
                redirectUris: new List<string> {
                    studentReactRootUrl,
                    $"{studentReactRootUrl}/auth/callback"
                },
                postLogoutRedirectUris: new List<string> { studentReactRootUrl },
                clientUri: studentReactRootUrl,
                logoUri: "/images/clients/react.svg"
            );
        }

        
        




        // Swagger Client
        var swaggerClientId = configurationSection["KnowledgeHub_Swagger:ClientId"];
        if (!swaggerClientId.IsNullOrWhiteSpace())
        {
            var swaggerRootUrl = configurationSection["KnowledgeHub_Swagger:RootUrl"]?.TrimEnd('/');

            await CreateOrUpdateApplicationAsync(
                applicationType: OpenIddictConstants.ApplicationTypes.Web,
                name: swaggerClientId!,
                type: OpenIddictConstants.ClientTypes.Public,
                consentType: OpenIddictConstants.ConsentTypes.Implicit,
                displayName: "Swagger Application",
                secret: null,
                grantTypes: new List<string> { OpenIddictConstants.GrantTypes.AuthorizationCode, },
                scopes: commonScopes,
                redirectUris: new List<string> { $"{swaggerRootUrl}/swagger/oauth2-redirect.html" },
                clientUri: swaggerRootUrl.EnsureEndsWith('/') + "swagger",
                logoUri: "/images/clients/swagger.svg"
            );
        }


    }

    private static List<string> GetConfiguredUrls(string? configuredUrls, string? fallbackUrl)
    {
        var urls = new List<string>();

        if (!configuredUrls.IsNullOrWhiteSpace())
        {
            foreach (var url in configuredUrls!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var normalized = NormalizeRedirectUrl(url);
                if (normalized != null && !urls.Contains(normalized))
                {
                    urls.Add(normalized);
                }
            }
        }

        if (urls.Count == 0 && !fallbackUrl.IsNullOrWhiteSpace())
        {
            var fallback = NormalizeRedirectUrl(fallbackUrl!);
            if (fallback != null)
            {
                urls.Add(fallback);
            }
        }

        return urls;
    }

    /// <summary>
    /// 归一化重定向地址。
    ///
    /// <para>
    /// 部署配置里经常把「学校专属域名」直接拼进重定向白名单（形如 <c>qidi.iketong.com</c>，
    /// 没有协议头）。OpenIddict 校验 RedirectUri 时要求绝对地址，裸域名会抛
    /// 「Invalid RedirectUri」，导致整个数据种子中断（连带角色权限没被播种）。
    /// 这里统一补成 <c>https://</c>；实在认不出来的直接跳过，不让一条脏配置打断部署。
    /// </para>
    /// </summary>
    private static string? NormalizeRedirectUrl(string url)
    {
        var value = url?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.TrimEnd('/');

        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        // 裸域名 / 主机名：补 https
        if (!value.Contains('/')
            && !value.Contains(' ')
            && value.Contains('.'))
        {
            return "https://" + value;
        }

        // 其余（相对路径等）OpenIddict 不认，跳过
        return null;
    }
}
