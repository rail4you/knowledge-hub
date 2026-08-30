using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub;

public class GrantAllPoliciesMiddleware : IMiddleware, ITransientDependency
{
    /// <summary>非学生业务角色：只要兼任其中之一，就不按"纯学生"处理。</summary>
    private static readonly string[] NonStudentRoles =
        { "Teacher", "SchoolAdmin", "LeagueAdmin", "EnterpriseUser", "admin" };

    /// <summary>
    /// 非联盟审核员角色：只要兼任其中之一，就不按"纯联盟审核员"处理。
    /// 纯联盟审核员（只有 LeagueAdmin 角色）不再注入全量权限，
    /// 只依赖数据库真实授权，保证其在界面只看到资源审核。
    /// </summary>
    private static readonly string[] NonLeagueOnlyRoles =
        { "Teacher", "SchoolAdmin", "EnterpriseUser", "admin" };

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // 拦截 application-configuration 响应
        var originalBody = context.Response.Body;
        using var newBody = new System.IO.MemoryStream();
        context.Response.Body = newBody;

        await next(context);

        if (context.Request.Path.Value?.Contains("/api/abp/application-configuration") == true
            && context.Response.StatusCode == 200)
        {
            newBody.Position = 0;
            var json = await new System.IO.StreamReader(newBody).ReadToEndAsync();
            
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement.Clone();
            
            // 读取已有 policies
            if (root.TryGetProperty("auth", out var auth) 
                && auth.TryGetProperty("grantedPolicies", out var policies))
            {
                var newPolicies = new Dictionary<string, object>();
                foreach (var p in policies.EnumerateObject())
                {
                    newPolicies[p.Name] = true;
                }

                // 学生角色：不注入任何额外权限，返回数据库中的真实权限，
                // 使其无法在页面/菜单上看到后台管理功能。
                // 纯联盟审核员（只有 LeagueAdmin 角色）：同样不注入额外权限，只依赖数据库授权，
                // 使其在界面只看到资源审核，避免越权看到院校审核（SchoolAudit）和其它后台管理。
                // 安全（fail-closed）：只有明确具备某个"非学生管理角色"的用户才注入全量后台权限。
                // 没有任何角色（含学生但角色声明缺失/未分配）的用户一律不注入，只保留数据库真实授权，
                // 避免无角色账号被错误地当成全量管理员。
                var hasAdminRole = NonStudentRoles.Any(context.User.IsInRole);
                if (hasAdminRole && !IsLeagueOnlyUser(context.User))
                {
                    // 注入完整的 KnowledgeHub 权限列表
                    // 注意：SchoolAudit/LeagueAudit/PhysicalDelete 等高权限不在此注入，全部依赖数据库授权，
                    // 确保院校审核员、联盟审核员、物理删除审批人彼此不可越权。
                    var allPerms = new[]
                    {
                    "KnowledgeHub.Resources", "KnowledgeHub.Resources.Create", "KnowledgeHub.Resources.Edit", "KnowledgeHub.Resources.Delete", "KnowledgeHub.Resources.Download",
                    "KnowledgeHub.Resources.ManageCategory",
                    "KnowledgeHub.Resources.RequestDelete", "KnowledgeHub.Resources.ViewStatistics", "KnowledgeHub.Resources.ViewRecommendation",
                    "KnowledgeHub.Search", "KnowledgeHub.Search.ManageIndex", "KnowledgeHub.Search.ViewStatistics", "KnowledgeHub.Search.ReviewResource",
                    "KnowledgeHub.Courses", "KnowledgeHub.Courses.Create", "KnowledgeHub.Courses.Edit", "KnowledgeHub.Courses.Delete", "KnowledgeHub.Courses.Enroll", "KnowledgeHub.Courses.ManageEnrollment",
                    "KnowledgeHub.AI", "KnowledgeHub.AI.Chat", "KnowledgeHub.AI.LessonPlan", "KnowledgeHub.AI.CaseAnalysis", "KnowledgeHub.AI.CareerGuidance",
                    "KnowledgeHub.TeachingAgents", "KnowledgeHub.TeachingAgents.Manage", "KnowledgeHub.TeachingAgents.Assign", "KnowledgeHub.TeachingAgents.Execute", "KnowledgeHub.TeachingAgents.Review",
                    "KnowledgeHub.Employment", "KnowledgeHub.Employment.PublishJob", "KnowledgeHub.Employment.ReviewJob", "KnowledgeHub.Employment.ManageResume",
                    "KnowledgeHub.Employment.ScheduleInterview", "KnowledgeHub.Employment.ManageGuidance", "KnowledgeHub.Employment.ManageOutcome",
                    "KnowledgeHub.Employment.ViewStatistics", "KnowledgeHub.Employment.ExportReport", "KnowledgeHub.Employment.ManageApplication", "KnowledgeHub.Employment.ViewMyApplication",
                    "KnowledgeHub.Practicum", "KnowledgeHub.Practicum.Create", "KnowledgeHub.Practicum.Edit", "KnowledgeHub.Practicum.Review", "KnowledgeHub.Practicum.Score", "KnowledgeHub.Practicum.Export", "KnowledgeHub.Practicum.ViewStatistics",
                    "KnowledgeHub.News", "KnowledgeHub.News.Create", "KnowledgeHub.News.Edit", "KnowledgeHub.News.Delete", "KnowledgeHub.News.Review", "KnowledgeHub.News.Publish", "KnowledgeHub.News.ManageComment",
                    "KnowledgeHub.MicroMajors", "KnowledgeHub.MicroMajors.Create", "KnowledgeHub.MicroMajors.Edit", "KnowledgeHub.MicroMajors.Delete",
                    "KnowledgeHub.MicroMajors.ManageEnrollment", "KnowledgeHub.MicroMajors.IssueCertificate", "KnowledgeHub.MicroMajors.ViewStatistics",
                    "KnowledgeHub.DoubleHigh", "KnowledgeHub.DoubleHigh.ManageProject", "KnowledgeHub.DoubleHigh.ManageIndicator", "KnowledgeHub.DoubleHigh.CollectData", "KnowledgeHub.DoubleHigh.ExportReport", "KnowledgeHub.DoubleHigh.ViewAll",
                    "KnowledgeHub.RecruitmentLive", "KnowledgeHub.RecruitmentLive.Create", "KnowledgeHub.RecruitmentLive.Manage",
                    "KnowledgeHub.Documents", "KnowledgeHub.Documents.Create", "KnowledgeHub.Documents.Edit", "KnowledgeHub.Documents.Delete",
                    "KnowledgeHub.Majors", "KnowledgeHub.Majors.Create", "KnowledgeHub.Majors.Edit", "KnowledgeHub.Majors.Delete",
                    "KnowledgeHub.Alliance", "KnowledgeHub.Alliance.Create", "KnowledgeHub.Alliance.Update", "KnowledgeHub.Alliance.Delete", "KnowledgeHub.Alliance.ManageMembers",
                    "KnowledgeHub.Learning", "KnowledgeHub.Learning.ViewStatistics", "KnowledgeHub.Learning.ExportData",
                    "KnowledgeHub.Users", "KnowledgeHub.Users.Create", "KnowledgeHub.Users.Edit", "KnowledgeHub.Users.Delete", "KnowledgeHub.Users.Import",
                    "AbpIdentity.Roles", "AbpIdentity.Roles.Create", "AbpIdentity.Roles.Update", "AbpIdentity.Roles.Delete", "AbpIdentity.Roles.ManagePermissions",
                    "AbpIdentity.Users", "AbpIdentity.Users.Create", "AbpIdentity.Users.Update", "AbpIdentity.Users.Delete", "AbpIdentity.Users.ManagePermissions", "AbpIdentity.Users.Update.ManageRoles",
                    // AbpTenantManagement.Tenants 不给非 host admin（从已有数据库权限获取）
                };

                foreach (var perm in allPerms)
                {
                    newPolicies[perm] = true;
                }

                // 移除租户管理权限（非 host admin 不应该看到）
                var tenantPerms = new[] { "AbpTenantManagement.Tenants", "AbpTenantManagement.Tenants.Create", "AbpTenantManagement.Tenants.Update", "AbpTenantManagement.Tenants.Delete", "AbpTenantManagement.Tenants.ManageFeatures", "AbpTenantManagement.Tenants.ManageConnectionStrings" };
                foreach (var tp in tenantPerms)
                {
                    newPolicies.Remove(tp);
                }
                }

                // 重建 JSON
                var ms = new System.IO.MemoryStream();
                var writer = new Utf8JsonWriter(ms);
                WriteJsonWithInjectedPolicies(root, newPolicies, writer);
                writer.Flush();
                ms.Position = 0;

                context.Response.Body = originalBody;
                context.Response.ContentLength = null;
                await ms.CopyToAsync(originalBody);
                return;
            }
        }

        newBody.Position = 0;
        await newBody.CopyToAsync(originalBody);
        context.Response.Body = originalBody;
    }

    private static bool IsLeagueOnlyUser(ClaimsPrincipal user)
    {
        if (!user.IsInRole("LeagueAdmin"))
        {
            return false;
        }

        return !NonLeagueOnlyRoles.Any(user.IsInRole);
    }

    private static void WriteJsonWithInjectedPolicies(JsonElement root, Dictionary<string, object> newPolicies, Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Name == "auth")
            {
                writer.WritePropertyName("auth");
                writer.WriteStartObject();
                foreach (var authProp in prop.Value.EnumerateObject())
                {
                    if (authProp.Name == "grantedPolicies")
                    {
                        writer.WritePropertyName("grantedPolicies");
                        writer.WriteStartObject();
                        foreach (var kv in newPolicies)
                        {
                            writer.WritePropertyName(kv.Key);
                            JsonSerializer.Serialize(writer, kv.Value);
                        }
                        writer.WriteEndObject();
                    }
                    else
                    {
                        authProp.WriteTo(writer);
                    }
                }
                writer.WriteEndObject();
            }
            else
            {
                prop.WriteTo(writer);
            }
        }
        writer.WriteEndObject();
    }
}
