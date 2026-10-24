using System;
using System.Threading.Tasks;
using KnowledgeHub.Schools;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.MultiTenancy;

/// <summary>
/// 域名 → 租户 解析贡献者（注册到解析链 <b>index 0</b>，域名优先于令牌）。
///
/// <para>
/// <b>为什么不用 ABP 自带的 <c>DomainTenantResolveContributor</c>：</b>
/// </para>
/// <list type="number">
/// <item><description>
/// ABP 版只支持「固定后缀 + 前缀即租户名」的字符串模板，表达不了「每校自己的独立域名」
/// （<c>www.qidi-edu.cn → qidi</c>），而这正是本项目的核心诉求。
/// </description></item>
/// <item><description>
/// ABP 的解析链是 <c>CurrentUser(令牌) → QueryString → Route → Header → Cookie</c>，
/// 域名解析器被插在令牌<b>之后</b>。持有 A 校令牌的教师访问 B 校域名时仍会看到 A 校内容，
/// 与「账户也是自己租户的」相悖。
/// </description></item>
/// </list>
///
/// <para>
/// 本贡献器置于最前，于是：
/// </para>
/// <list type="bullet">
/// <item><description>游客访问学校域名 → 自动落入该租户，只看到本校内容；</description></item>
/// <item><description>令牌租户与域名租户不一致 → OpenIddict 校验令牌租户失败 → 令牌失效 → 前端引导重新登录；</description></item>
/// <item><description>平台域名（如 <c>www.iketong.com</c>）不匹配任何学校 → 不处理，交回 ABP 默认链，行为与今天完全一致。</description></item>
/// </list>
///
/// <para>
/// 依赖从 <see cref="ITenantResolveContext.ServiceProvider"/>（每次解析新建的 scope）解析，
/// 避免把 scoped 仓储捕获到启动期创建的单例解析器里。
/// </para>
/// </summary>
public class SchoolDomainTenantResolveContributor : TenantResolveContributorBase, ITransientDependency
{
    public override string Name => ISchoolDomainResolver.ResolverName;

    public override async Task ResolveAsync(ITenantResolveContext context)
    {
        var httpContext = context.ServiceProvider.GetService<IHttpContextAccessor>()?.HttpContext;
        if (httpContext == null)
        {
            return;
        }

        // 只取主机名（不含端口）：本地开发 qidi.localhost:4200 也能正确解析
        var host = School.NormalizeDomain(httpContext.Request.Host.Host);
        if (string.IsNullOrWhiteSpace(host))
        {
            return;
        }

        var resolver = context.ServiceProvider.GetService<ISchoolDomainResolver>();
        if (resolver == null)
        {
            return;
        }

        var tenantId = await resolver.ResolveTenantIdAsync(host);
        if (tenantId == null)
        {
            // 未命中：不设置 Handled，交回 ABP 默认解析链（令牌 / __tenant 头 / Cookie）
            return;
        }

        // Handled=true 表示「已解析」，TenantIdOrName 传 Guid 字符串 → ABP 按租户 Id 查找
        context.TenantIdOrName = tenantId.Value.ToString();
        context.Handled = true;
    }
}
