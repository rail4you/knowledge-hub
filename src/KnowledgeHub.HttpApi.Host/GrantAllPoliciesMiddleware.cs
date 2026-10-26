using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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

    private readonly IRevokedPermissionTracker _revokedPermissionTracker;
    private readonly ILogger<GrantAllPoliciesMiddleware> _logger;

    public GrantAllPoliciesMiddleware(
        IRevokedPermissionTracker revokedPermissionTracker,
        ILogger<GrantAllPoliciesMiddleware> logger)
    {
        _revokedPermissionTracker = revokedPermissionTracker;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // SSE 长连接（如 /api/learning/practicum-chat/stream/{id}）必须直通：
        // 下方会把 Response.Body 换成 MemoryStream 并在 next() 返回后才回拷，
        // 而 SSE 的 Action 永不返回（直到客户端断开），会导致首个 data: 事件
        // 永远发不出去，前端 EventSource 一直停在 CONNECTING（页面显示"连接中"）。
        // 这类端点与 application-configuration 无关，直接放行。

        // 浏览器 EventSource 无法设置 Authorization 头，允许 SSE 端点通过
        // ?access_token=xxx 传递 JWT；这里在认证中间件之前把它复制为标准头。
        // 同理，/api/resource-file/ 的下载/预览/缩略图常由 <a download>、<img>、
        // <video>、window.location 等浏览器原生请求触发（无法设置请求头），
        // 而登录改为纯 Angular 密码授权后访问令牌只存在内存、浏览器不再持有
        // 认证 cookie，因此也必须支持 ?access_token= 透传，否则这些请求会
        // 302 跳登录页导致下载/预览失败。
        // resume-preview 同理：简历附件预览由 <iframe src> 与 window.open 触发
        //（浏览器原生导航，无法设置请求头），且该端点带 [Authorize]，必须透传 token。
        // AI 生成媒体同理：<img>/<video> 预览下载为浏览器原生请求，且持久化地址是
        // https://API域名/uploads/... 绝对地址，开发环境自签名证书会被浏览器拒绝，
        // 必须改走同源 /api/learning/ai/media-file。
        var path = context.Request.Path.Value;
        var supportsTokenViaQuery =
            path != null &&
            (path.Contains("/practicum-chat/stream", System.StringComparison.OrdinalIgnoreCase) ||
             path.StartsWith("/api/resource-file/", System.StringComparison.OrdinalIgnoreCase) ||
             path.Equals("/api/app/resume-preview", System.StringComparison.OrdinalIgnoreCase) ||
             path.StartsWith("/api/learning/ai/media-file", System.StringComparison.OrdinalIgnoreCase));
        if (supportsTokenViaQuery &&
            !context.Request.Headers.ContainsKey("Authorization"))
        {
            var accessToken = context.Request.Query["access_token"].FirstOrDefault();
            if (!string.IsNullOrEmpty(accessToken))
            {
                context.Request.Headers.Authorization = "Bearer " + accessToken;
            }
        }

        if (path != null &&
            (path.Contains("/stream", System.StringComparison.OrdinalIgnoreCase)
             || path.Contains("/practicum-chat/", System.StringComparison.OrdinalIgnoreCase)
             // 资源文件预览/下载/缩略图必须直通：
             // 下方会把整个响应体缓冲进 MemoryStream 再回拷，大视频（上百 MB）
             // 会直接缓冲失败导致 500（线上实测：150MB+ 视频整文件 GET 必 500，
             // 小 Range 分片反而 206 正常；9MB PDF 整文件正常）。
             // 直通后 PhysicalFile + enableRangeProcessing 直接流式输出，<video> 边下边播。
             // 注意：上方的 ?access_token= 透传已在此之前完成，不受直通影响。
             || path.StartsWith("/api/resource-file/", System.StringComparison.OrdinalIgnoreCase)
             // 岗位视频同理：<video src> 原生请求且需 Range 拖动，禁止整响应缓冲。
              || path.StartsWith("/api/app/job-video", System.StringComparison.OrdinalIgnoreCase)
              // AI 生成媒体（教学图片/短视频）同理：PhysicalFile + enableRangeProcessing
              // 需要直连响应体，缓冲会破坏 Range 206 分片。
              || path.StartsWith("/api/learning/ai/media-file", System.StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        // 拦截 application-configuration 响应。
        // 注意：next() 抛出时也必须还原 Response.Body，否则异常处理中间件重入管线
        // 会拿到外层已释放的 MemoryStream（ObjectDisposedException），导致任何普通异常
        //（如登录校验失败）都变成空白 500 错误页。
        var originalBody = context.Response.Body;
        using var newBody = new System.IO.MemoryStream();
        context.Response.Body = newBody;

        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        if (context.Request.Path.Value?.Contains("/api/abp/application-configuration") == true
            && context.Response.StatusCode == 200)
        {
            newBody.Position = 0;
            var json = await new System.IO.StreamReader(newBody).ReadToEndAsync();

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(json);
            }
            catch (JsonException)
            {
                // 响应体不是合法 JSON（为空 / 被响应压缩中间件处理过 / 其它中间件改写过）。
                // 这里绝不能抛异常：这个接口是前端启动的前置依赖，一旦 500，
                // 整个应用会一直停在启动闪屏。降级为「原样回写」并去掉编码头，
                // 保证客户端至少能拿到一份可解析（哪怕未注入权限）的响应。
                newBody.Position = 0;
                context.Response.Headers.Remove("Content-Encoding");
                context.Response.ContentLength = null;
                await newBody.CopyToAsync(originalBody);
                context.Response.Body = originalBody;
                return;
            }

            using var docScope = doc;
            var root = docScope.RootElement.Clone();
            
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
                    // 运营在「角色 → 权限」里显式取消过的权限：兜底注入时要跳过。
                    var revokedPermissions = await GetRevokedPermissionsAsync(context, root);

                    // 是否允许兜底注入某个权限。
                    // 被运营显式取消、且数据库里确实没有该权限的，一律不注入
                    //（否则菜单会显示、点进去 403 —— "菜单显示应该和权限有关"就不成立）。
                    // 数据库里已有的说明用户还通过别的角色持有它，保留。
                    bool ShouldInject(string perm)
                        => !revokedPermissions.Contains(perm) || policies.TryGetProperty(perm, out _);

                    // 注入完整的 KnowledgeHub 权限列表
                    // 注意：SchoolAudit/LeagueAudit/PhysicalDelete 等高权限不在此注入，全部依赖数据库授权，
                    // 确保院校审核员、联盟审核员、物理删除审批人彼此不可越权。
                    var allPerms = new[]
                    {
                    "KnowledgeHub.Resources", "KnowledgeHub.Resources.Create", "KnowledgeHub.Resources.Edit", "KnowledgeHub.Resources.Delete", "KnowledgeHub.Resources.Download",
                    "KnowledgeHub.Resources.ManageCategory",
                    "KnowledgeHub.Resources.RequestDelete", "KnowledgeHub.Resources.ViewStatistics", "KnowledgeHub.Resources.ViewRecommendation",
                    // 资源共享：让所有非学生管理角色（SchoolAdmin/Teacher 等）在前端能看到"共享资源"按钮和"我共享的"Tab，
                    // 后端 AppService 会按 [Authorize] 二次校验真实授权，避免越权。
                    "KnowledgeHub.Courses.Share", "KnowledgeHub.Courses.ManageShare",
                    "KnowledgeHub.Search", "KnowledgeHub.Search.ManageIndex", "KnowledgeHub.Search.ViewStatistics", "KnowledgeHub.Search.ReviewResource",
                    "KnowledgeHub.Courses", "KnowledgeHub.Courses.Create", "KnowledgeHub.Courses.Edit", "KnowledgeHub.Courses.Delete", "KnowledgeHub.Courses.Enroll", "KnowledgeHub.Courses.ManageEnrollment",
                    "KnowledgeHub.AI", "KnowledgeHub.AI.Chat", "KnowledgeHub.AI.LessonPlan", "KnowledgeHub.AI.CaseAnalysis", "KnowledgeHub.AI.CareerGuidance", "KnowledgeHub.AI.ExerciseGenerate",
                    "KnowledgeHub.AI.ImageGeneration", "KnowledgeHub.AI.VideoGeneration",
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
                    // 注意：Alliance.*（联盟管理）是 host 全局能力，仅 host 用户注入，见下方；
                    // 租户管理员（SchoolAdmin）不得持有，避免越权。LeagueAudit 同理不注入，依赖数据库授权。
                    "KnowledgeHub.Learning", "KnowledgeHub.Learning.ViewStatistics", "KnowledgeHub.Learning.ExportData",
                    "KnowledgeHub.Users", "KnowledgeHub.Users.Create", "KnowledgeHub.Users.Edit", "KnowledgeHub.Users.Delete", "KnowledgeHub.Users.Import",
                    // AbpIdentity.*（角色/用户管理）不在此注入：属于高权限能力，
                    // 只允许宿主 admin 与租户院校管理员（SchoolAdmin）持有，见下方按角色显式处理。
                    // AbpTenantManagement.Tenants 的处理见下方：仅 host 全局 admin 注入，其余剥离。
                };

                foreach (var perm in allPerms)
                {
                    // 兜底注入前先排除「运营在角色权限里显式取消过」的权限。
                    // 否则运营取消了某项权限，菜单却仍然显示（点进去 403），
                    // 「菜单显示应该和权限有关」就无法成立。
                    if (!ShouldInject(perm))
                    {
                        continue;
                    }

                    newPolicies[perm] = true;
                }

                // 联盟管理（全局能力）：仅 host 用户（全局 admin / 联盟管理员）注入；
                // 租户用户（SchoolAdmin 等）剥离，菜单自动隐藏，后端鉴权亦为 false（Host-only 权限）。
                var alliancePerms = new[] { "KnowledgeHub.Alliance", "KnowledgeHub.Alliance.Create", "KnowledgeHub.Alliance.Update", "KnowledgeHub.Alliance.Delete", "KnowledgeHub.Alliance.ManageMembers" };
                if (IsHostUser(root))
                {
                    foreach (var ap in alliancePerms)
                    {
                        // 同样尊重运营的显式取消：取消后不兜底注入（否则菜单显示但接口 403）。
                        if (!ShouldInject(ap))
                        {
                            continue;
                        }

                        newPolicies[ap] = true;
                    }
                }
                else
                {
                    foreach (var ap in alliancePerms)
                    {
                        newPolicies.Remove(ap);
                    }
                }

                // 租户管理（新建租户）菜单：仅 host 全局 admin 可见。
                // host 与否以 application-configuration 中 currentUser.tenantId 为准
                //（与前端 hostOnlyGuard / 身份页用的是同一信号）：
                // host admin 注入，确保超级管理员菜单恢复；
                // 租户用户（教师端/租户管理员）剥离，菜单自动隐藏。
                var tenantPerms = new[] { "AbpTenantManagement.Tenants", "AbpTenantManagement.Tenants.Create", "AbpTenantManagement.Tenants.Update", "AbpTenantManagement.Tenants.Delete", "AbpTenantManagement.Tenants.ManageFeatures", "AbpTenantManagement.Tenants.ManageConnectionStrings" };
                if (IsHostUser(root) && context.User.IsInRole("admin"))
                {
                    foreach (var tp in tenantPerms)
                    {
                        if (!ShouldInject(tp))
                        {
                            continue;
                        }

                        newPolicies[tp] = true;
                    }
                }
                else
                {
                    foreach (var tp in tenantPerms)
                    {
                        newPolicies.Remove(tp);
                    }
                }

                // 身份管理（角色/用户）权限：高权限能力，不随上面的全量后台权限注入。
                // 仅宿主 admin 与租户院校管理员（SchoolAdmin）持有 —— 与 RolePermissionSeeder
                // 的数据库授权保持一致；Teacher / Student / EnterpriseUser / LeagueAdmin
                // 一律剥离，确保教师登录后看不到「身份管理 → 角色管理」菜单，
                // 直接访问 /identity/roles 也会被 permissionGuard 拦下。
                var identityPerms = new[]
                {
                    "AbpIdentity.Roles", "AbpIdentity.Roles.Create", "AbpIdentity.Roles.Update", "AbpIdentity.Roles.Delete", "AbpIdentity.Roles.ManagePermissions",
                    "AbpIdentity.Users", "AbpIdentity.Users.Create", "AbpIdentity.Users.Update", "AbpIdentity.Users.Delete", "AbpIdentity.Users.ManagePermissions", "AbpIdentity.Users.Update.ManageRoles",
                };
                if (context.User.IsInRole("admin") || context.User.IsInRole("SchoolAdmin"))
                {
                    foreach (var ip in identityPerms)
                    {
                        // 运营取消后不兜底注入：菜单必须跟着权限走。
                        if (!ShouldInject(ip))
                        {
                            continue;
                        }

                        newPolicies[ip] = true;
                    }
                }
                else
                {
                    foreach (var ip in identityPerms)
                    {
                        newPolicies.Remove(ip);
                    }
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

    /// <summary>
    /// 取当前用户的角色名。
    ///
    /// <para>
    /// **不能直接用 <see cref="ClaimTypes.Role"/>**：JWT 里角色 claim 的类型取决于颁发器的
    /// <c>RoleClaimType</c>（OpenIddict / ASP.NET Core 默认是短名 "role"，
    /// 而 <see cref="ClaimTypes.Role"/> 是长 URI "http://www.aspnet.org/claims/role"），
    /// 用错 claim 类型会取到空集合 —— 于是「运营显式取消」的标记永远查不到，
    /// 取消标记检查被整体跳过，中间件把权限原样兜底注入，
    /// 结果就是「在角色权限里取消了，菜单还是删不掉」。
    /// </para>
    ///
    /// <para>
    /// 这里按每个 identity 自己的 RoleClaimType 取，并兼容短名 "role" 与长 URI。
    /// <c>IsInRole</c> 之所以能用，是因为它走的是 identity.RoleClaimType。
    /// </para>
    /// </summary>
    private static List<string> GetRoleNames(ClaimsPrincipal user)
    {
        var roleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (user == null)
        {
            return new List<string>();
        }

        foreach (var identity in user.Identities)
        {
            var claimTypes = new List<string>();
            if (!string.IsNullOrEmpty(identity.RoleClaimType))
            {
                claimTypes.Add(identity.RoleClaimType);
            }

            claimTypes.Add(ClaimTypes.Role);
            claimTypes.Add("role");

            foreach (var claimType in claimTypes.Distinct())
            {
                foreach (var claim in identity.FindAll(claimType))
                {
                    if (!string.IsNullOrWhiteSpace(claim.Value))
                    {
                        roleNames.Add(claim.Value);
                    }
                }
            }
        }

        return roleNames.ToList();
    }

    /// <summary>
    /// 取当前用户在其所属租户下被运营**显式取消**过的权限集合。
    ///
    /// <para>
    /// 租户 id 取自 application-configuration 的 currentUser.tenantId（与 <see cref="IsHostUser"/> 同源）：
    /// 本中间件在 UseMultiTenancy 之前执行，next() 返回后租户 scope 已释放，
    /// ICurrentTenant 不可靠，只能从响应体里取。
    /// </para>
    /// </summary>
    private async Task<HashSet<string>> GetRevokedPermissionsAsync(HttpContext context, JsonElement root)
    {
        try
        {
            var roleNames = GetRoleNames(context.User);

            if (roleNames.Count == 0)
            {
                _logger.LogDebug("[GrantAllPolicies] 未取到任何角色声明，跳过取消标记检查。");
                return new HashSet<string>();
            }

            Guid? tenantId = null;
            if (root.TryGetProperty("currentUser", out var currentUser) &&
                currentUser.ValueKind == JsonValueKind.Object &&
                currentUser.TryGetProperty("tenantId", out var tenantIdElement) &&
                tenantIdElement.ValueKind == JsonValueKind.String &&
                Guid.TryParse(tenantIdElement.GetString(), out var parsedTenantId))
            {
                tenantId = parsedTenantId;
            }

            var revoked = await _revokedPermissionTracker.GetRevokedPermissionsAsync(roleNames, tenantId);
            _logger.LogDebug(
                "[GrantAllPolicies] 取消标记检查：租户={TenantId} 角色={Roles} 命中={Count} 项。",
                tenantId?.ToString() ?? "host", string.Join(",", roleNames), revoked.Count);
            return revoked;
        }
        catch (Exception ex)
        {
            // 取不到取消标记时按"没有取消"处理（保持原有兜底行为，不影响正常用户）。
            // 但必须留下日志：否则"运营取消了权限、菜单却仍然显示"这类问题无从排查。
            _logger.LogError(ex, "[GrantAllPolicies] 读取取消标记失败，本次不跳过任何权限。");
            return new HashSet<string>();
        }
    }

    /// <summary>
    /// 以 application-configuration 中的 currentUser.tenantId 判断是否为 host 全局用户。
    /// 缺失或为 null 即 host（与前端 hostOnlyGuard / 身份页用的是同一信号）。
    /// 用 JSON 而不用 ICurrentTenant：本中间件在 UseMultiTenancy 之前执行，
    /// next() 返回后租户 scope 可能已释放，ICurrentTenant 不可靠。
    /// </summary>
    private static bool IsHostUser(JsonElement root)
    {
        if (root.TryGetProperty("currentUser", out var currentUser) &&
            currentUser.ValueKind == JsonValueKind.Object &&
            currentUser.TryGetProperty("tenantId", out var tenantId))
        {
            if (tenantId.ValueKind == JsonValueKind.Null || tenantId.ValueKind == JsonValueKind.Undefined)
            {
                return true;
            }
            var tenantIdString = tenantId.ValueKind == JsonValueKind.String
                ? tenantId.GetString()
                : tenantId.ToString();
            return string.IsNullOrEmpty(tenantIdString);
        }
        return true;
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
