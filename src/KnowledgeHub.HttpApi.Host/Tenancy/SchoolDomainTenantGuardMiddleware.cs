using System;
using System.Text.Json;
using System.Threading.Tasks;
using KnowledgeHub.Schools;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace KnowledgeHub.MultiTenancy;

/// <summary>
/// 学校域名守卫：令牌所属租户与域名解析出的租户不一致时，直接拒绝。
///
/// <para>
/// <b>为什么需要这道守卫：</b>
/// 本项目的管线顺序是 <c>UseAuthentication → UseAbpOpenIddictValidation → UseMultiTenancy</c>，
/// 也就是<b>令牌先校验、租户后解析</b>。因此 OpenIddict 校验令牌时上下文里还没有租户，
/// 跨校令牌（A 校令牌访问 B 校域名）不会被判为无效 —— 它会带着 A 校的 CurrentUser 进入 B 校上下文。
/// </para>
///
/// <para>
/// 数据层面并不会泄露：所有查询都按当前租户（域名解析结果）过滤，他校令牌读不到他校数据，
/// 只会得到「实体不存在」这类 404。但对用户来说这是莫名其妙的错误。
/// 这里显式拦下来，给出「该账号属于其它学校，请到对应学校域名登录」的明确提示。
/// </para>
///
/// <para>
/// 判定依据：当前请求的 <b>Host 能否解析出某所学校</b>（<see cref="ISchoolDomainContext"/>）——
/// 平台域名下不触发本守卫，行为与改造前完全一致。
/// 不能用 <c>ITenantResolveResultAccessor.Result.AppliedResolvers</c>：ABP 会把
/// <b>每个尝试过的</b>解析器名字都记进去，该判断在平台域名下恒为真，
/// 会把「平台域名 + 残留 __tenant cookie」的正常请求误判成跨校访问。
/// </para>
/// </summary>
public class SchoolDomainTenantGuardMiddleware : IMiddleware, ITransientDependency
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var schoolDomainContext = context.RequestServices.GetRequiredService<ISchoolDomainContext>();
        var resolvedByDomain = await schoolDomainContext.IsCurrentHostSchoolDomainAsync();
        var currentTenant = context.RequestServices.GetRequiredService<ICurrentTenant>();
        var currentUser = context.RequestServices.GetRequiredService<ICurrentUser>();

        if (resolvedByDomain
            && currentTenant.Id.HasValue
            && currentUser.IsAuthenticated
            && currentUser.TenantId != currentTenant.Id)
        {
            await WriteTenantMismatchAsync(context, currentUser.TenantId, currentTenant.Id.Value);
            return;
        }

        await next(context);
    }

    private static async Task WriteTenantMismatchAsync(HttpContext context, Guid? tokenTenantId, Guid currentTenantId)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json; charset=utf-8";

        var message = tokenTenantId.HasValue
            ? "当前登录账号不属于该学校，请使用该学校的账号登录，或前往对应学校的域名登录。"
            : "系统管理员请在平台域名登录后再访问学校专属站点。";

        var payload = new
        {
            error = new
            {
                code = (string?)null,
                message,
                details = (string?)null,
                data = (object?)null,
                validationErrors = Array.Empty<object>()
            }
        };

        await JsonSerializer.SerializeAsync(context.Response.Body, payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}
