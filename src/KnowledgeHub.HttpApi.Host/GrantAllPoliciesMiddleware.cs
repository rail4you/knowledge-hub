using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub;

public class GrantAllPoliciesMiddleware : IMiddleware, ITransientDependency
{
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

                // 注入完整的 KnowledgeHub 权限列表
                var allPerms = new[]
                {
                    "KnowledgeHub.Resources", "KnowledgeHub.Resources.Create", "KnowledgeHub.Resources.Edit", "KnowledgeHub.Resources.Delete", "KnowledgeHub.Resources.Download",
                    "KnowledgeHub.Resources.SchoolAudit", "KnowledgeHub.Resources.LeagueAudit", "KnowledgeHub.Resources.ManageCategory",
                    "KnowledgeHub.Resources.RequestDelete", "KnowledgeHub.Resources.PhysicalDelete", "KnowledgeHub.Resources.ViewStatistics", "KnowledgeHub.Resources.ViewRecommendation",
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
                    "KnowledgeHub.TenantInfo", "KnowledgeHub.TenantInfo.Edit",
                    "KnowledgeHub.Users", "KnowledgeHub.Users.Create", "KnowledgeHub.Users.Edit", "KnowledgeHub.Users.Delete", "KnowledgeHub.Users.Import",
                    "AbpIdentity.Roles", "AbpIdentity.Roles.Create", "AbpIdentity.Roles.Update", "AbpIdentity.Roles.Delete", "AbpIdentity.Roles.ManagePermissions",
                    "AbpIdentity.Users", "AbpIdentity.Users.Create", "AbpIdentity.Users.Update", "AbpIdentity.Users.Delete", "AbpIdentity.Users.ManagePermissions", "AbpIdentity.Users.Update.ManageRoles",
                    "AbpTenantManagement.Tenants", "AbpTenantManagement.Tenants.Create", "AbpTenantManagement.Tenants.Update", "AbpTenantManagement.Tenants.Delete",
                    "FeatureManagement.ManageHostFeatures", "SettingManagement.Emailing", "SettingManagement.TimeZone",
                };

                foreach (var perm in allPerms)
                {
                    newPolicies[perm] = true;
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
