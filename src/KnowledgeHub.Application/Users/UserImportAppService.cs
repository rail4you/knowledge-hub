using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using KnowledgeHub.Majors;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;

namespace KnowledgeHub.Users;

[Authorize(KnowledgeHubPermissions.Users.Import)]
public class UserImportAppService : KnowledgeHubAppService, IUserImportAppService
{
    private const string MajorIdExtraProperty = "MajorId";

    private readonly IIdentityUserRepository _identityUserRepository;
    private readonly IRepository<Volo.Abp.Identity.IdentityUser, Guid> _userRepository;
    private readonly IdentityUserManager _identityUserManager;
    private readonly IIdentityRoleRepository _identityRoleRepository;
    private readonly IMajorRepository _majorRepository;
    private readonly IPermissionManager _permissionManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICurrentTenant _currentTenant;

    private static readonly Dictionary<string, UserRoleType> RoleDisplayNameMapping = new()
    {
        { "联盟管理员", UserRoleType.LeagueAdmin },
        { "院校管理员", UserRoleType.SchoolAdmin },
        { "教师", UserRoleType.Teacher },
        { "学生", UserRoleType.Student },
        { "企业用户", UserRoleType.EnterpriseUser },
    };

    private static readonly Dictionary<UserRoleType, string> RoleTypeDisplayNames = new()
    {
        { UserRoleType.LeagueAdmin, "联盟管理员" },
        { UserRoleType.SchoolAdmin, "院校管理员" },
        { UserRoleType.Teacher, "教师" },
        { UserRoleType.Student, "学生" },
        { UserRoleType.EnterpriseUser, "企业用户" },
    };

    /// <summary>
    /// 各角色类型 → 必填列名（与 UserImportTemplateHeaders 列名严格对应）。
    /// 注意：跨角色共有字段（姓名/登录账号/初始密码/手机号）由通用校验处理，不在此重复列出。
    /// </summary>
    private static readonly Dictionary<UserRoleType, List<string>> RequiredFieldsMapping = new()
    {
        { UserRoleType.LeagueAdmin, new List<string> { "工号" } },
        { UserRoleType.SchoolAdmin, new List<string> { "工号" } },
        { UserRoleType.Teacher, new List<string> { "工号", "所属院系/部门", "专业" } },
        { UserRoleType.Student, new List<string> { "学号", "年级", "班级", "专业" } },
        { UserRoleType.EnterpriseUser, new List<string> { "邮箱", "企业名称", "职位/岗位" } },
    };

    /// <summary>
    /// 用户导入模板表头（单 Sheet）。
    /// 与 ParseRow 读取的列顺序严格对应；前 5 列（角色类型/姓名/登录账号/初始密码/手机号）为通用必填，
    /// 第 6 列（邮箱）默认为可选，仅 企业用户 必填；其它列按角色按需填写。
    /// 最后一列"租户名称"仅 host 管理员导入时使用：留空表示导入到 host 全局用户。
    /// </summary>
    private static readonly string[] UserImportTemplateHeaders =
    {
        "角色类型", "姓名", "登录账号", "初始密码", "手机号", "邮箱",
        "所属院校", "工号", "所属院系/部门", "专业", "所教课程", "职称",
        "学号", "年级", "班级", "管理范围", "企业名称",
        "职位/岗位", "行业", "合作学校", "备注", "租户名称"
    };

    private static readonly XLColor RequiredHeaderColor = XLColor.FromArgb(255, 244, 230);
    private static readonly XLColor OptionalHeaderColor = XLColor.FromArgb(232, 244, 255);
    private static readonly string DefaultSheetName = "用户导入";

    public UserImportAppService(
        IIdentityUserRepository identityUserRepository,
        IRepository<Volo.Abp.Identity.IdentityUser, Guid> userRepository,
        IdentityUserManager identityUserManager,
        IIdentityRoleRepository identityRoleRepository,
        IMajorRepository majorRepository,
        IPermissionManager permissionManager,
        ITenantRepository tenantRepository,
        ICurrentTenant currentTenant)
    {
        _identityUserRepository = identityUserRepository;
        _userRepository = userRepository;
        _identityUserManager = identityUserManager;
        _identityRoleRepository = identityRoleRepository;
        _majorRepository = majorRepository;
        _permissionManager = permissionManager;
        _tenantRepository = tenantRepository;
        _currentTenant = currentTenant;
    }

    // ──────────────────────────── 公开 API ────────────────────────────

    /// <summary>
    /// 解析 Excel 文件并返回每行的预览结果（新建/覆盖/跳过/失败），
    /// 不写入数据库；用户确认后再调用 <see cref="ImportAsync"/> 实际落地。
    /// 自动路由：POST /api/app/user-import/preview（方法名 PreviewAsync 去掉 Async 后缀转 kebab-case）。
    /// [IgnoreAntiforgeryToken] 会被 ABP conventional controller 继承，避免浏览器 POST 被 antiforgery 拦截。
    /// </summary>
    [IgnoreAntiforgeryToken]
    public async Task<UserImportResultDto> PreviewAsync(ImportUsersFileDto input)
    {
        var rows = await ParseFileAsync(input);
        // 透传 input.OverwriteExisting：预览必须如实反映“勾选覆盖后会发生什么”，
        // 用户才能在确认前看到 新建/覆盖/跳过 的真实分布。
        var result = await BuildResultAsync(rows, overwriteExisting: input.OverwriteExisting, isPreview: true);
        return result;
    }

    /// <summary>
    /// 实际导入：根据 <see cref="ImportUsersFileDto.OverwriteExisting"/> 决定遇到同名用户时是覆盖还是跳过。
    /// 行为：单事务内尽力而为；任意一行失败不会回滚其他行，结果通过 Items 返回。
    /// 自动路由：POST /api/app/user-import/import。
    /// </summary>
    [IgnoreAntiforgeryToken]
    public async Task<UserImportResultDto> ImportAsync(ImportUsersFileDto input)
    {
        var rows = await ParseFileAsync(input);
        var result = await BuildResultAsync(rows, overwriteExisting: input.OverwriteExisting, isPreview: false);
        return result;
    }

    // ────────────────────── 解析 & 预览 & 落库 核心 ──────────────────────

    /// <summary>
    /// 解析 Excel → 标准化 ParsedRow 列表（含每行的角色 / 字段 / 解析错误）。
    /// 不做业务校验、不查重复、不写库；同时被 Preview / Import 共用。
    /// </summary>
    private async Task<List<ParsedImportRow>> ParseFileAsync(ImportUsersFileDto input)
    {
        if (string.IsNullOrWhiteSpace(input.FileBase64))
        {
            throw new UserFriendlyException("请先选择要导入的 Excel 文件。");
        }

        byte[] excelFile;
        try
        {
            excelFile = Convert.FromBase64String(input.FileBase64);
        }
        catch (FormatException)
        {
            throw new UserFriendlyException("文件内容不是有效的 Excel 数据，请重新选择文件后上传。");
        }

        // 解析 Excel 中所有可识别的行：兼容新旧模板（第 1 行标题 / 第 2 行说明 / 第 3 行表头）
        // 与旧模板（第 1 行即表头）；找不到表头时按旧行为默认跳过第 1 行。
        List<ParsedImportRow> allRows;
        using (var stream = new MemoryStream(excelFile))
        using (var workbook = new XLWorkbook(stream))
        {
            var worksheet = workbook.Worksheet(DefaultSheetName) ?? workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
            {
                throw new UserFriendlyException($"未找到工作表（{DefaultSheetName}），请使用最新模板。");
            }

            var usedRows = worksheet.RangeUsed()?.RowsUsed().ToList();
            if (usedRows == null || usedRows.Count == 0)
            {
                return new List<ParsedImportRow>();
            }

            var headerRowNumber = FindHeaderRowNumber(usedRows);
            var dataRows = usedRows.Where(r => r.RowNumber() > headerRowNumber).ToList();
            allRows = new List<ParsedImportRow>(dataRows.Count);

            foreach (var row in dataRows)
            {
                var parsed = ParseSingleRow(row, headerRowNumber);
                if (parsed != null)
                {
                    allRows.Add(parsed);
                }
            }
        }

        // 解决租户名称 → TenantId（host 管理员才需要；租户管理员永远用自己所在租户）
        await ResolveTenantsAsync(allRows, input.TenantId);

        return allRows;
    }

    /// <summary>
    /// 校验 + 落库（或仅预览），返回前端可直接渲染的 UserImportResultDto。
    /// 同一方法被 Preview / Import 共用，差异仅在 isPreview 与 overwriteExisting。
    /// </summary>
    private async Task<UserImportResultDto> BuildResultAsync(
        List<ParsedImportRow> parsedRows,
        bool overwriteExisting,
        bool isPreview)
    {
        var result = new UserImportResultDto();

        // 1) 预先缓存"已有用户名 → 用户"映射，用于一次查询完成所有行的重复检测
        var userNameToExistingUser = await GetExistingUserLookupAsync(parsedRows);

        // 2) 文件内去重：同一文件出现两次相同（登录账号 + 目标租户）时，
        //    后出现的行直接跳过，避免导入时第二行撞库产生难懂的 Identity 错误。
        //    key → 首次出现的行号（仅校验通过的行才计入）。
        var seenInFile = new Dictionary<(string UserName, Guid? TenantId), int>();

        foreach (var pr in parsedRows)
        {
            var item = new UserImportPreviewItemDto
            {
                RowNumber = pr.RowNumber,
                RoleType = pr.RoleType,
                RoleDisplayName = RoleTypeDisplayNames.GetValueOrDefault(pr.RoleType, pr.RoleType.ToString()),
                UserName = pr.Parsed?.UserName ?? string.Empty,
                Name = pr.Parsed?.Name ?? string.Empty,
                PhoneNumber = pr.Parsed?.PhoneNumber ?? string.Empty,
                Email = pr.Parsed?.Email ?? string.Empty,
                TenantName = pr.Parsed?.TenantName,
            };

            // 解析阶段就失败（如：角色类型无效 / 通用必填缺失）
            if (pr.ParseError != null)
            {
                item.Status = UserImportItemStatus.Fail;
                item.Reason = pr.ParseError;
                AddItem(result, item);
                continue;
            }

            // 业务校验（角色专属必填 + 租户分配合法性）
            var validationError = ValidateBusinessRules(pr);
            if (validationError != null)
            {
                item.Status = UserImportItemStatus.Fail;
                item.Reason = validationError;
                AddItem(result, item);
                continue;
            }

            // 学生专业存在性预检（预览与导入共用，保证两者结论一致）。
            // 若留到建用户时才检查，预览会显示 NEW 而实际导入失败，用户无法信任预览。
            if (pr.Parsed!.RoleType == UserRoleType.Student && !string.IsNullOrWhiteSpace(pr.Parsed.Major))
            {
                var majorExists = await _majorRepository.FindByNameAsync(pr.Parsed.Major) != null;
                if (!majorExists)
                {
                    item.Status = UserImportItemStatus.Fail;
                    item.Reason = $"专业【{pr.Parsed.Major}】不存在，请先在专业管理中创建";
                    AddItem(result, item);
                    continue;
                }
            }

            // 文件内重复检测（校验通过后才计入 seen，避免失败行污染后续行）
            var fileKey = (NormalizeUserName(pr.Parsed!.UserName), pr.EffectiveTenantId);
            if (seenInFile.TryGetValue(fileKey, out var firstRowNumber))
            {
                item.Status = UserImportItemStatus.Skip;
                item.Reason = $"与第 {firstRowNumber} 行登录账号重复（同一租户），已跳过";
                result.SkipCount++;
                AddItem(result, item);
                continue;
            }
            seenInFile[fileKey] = pr.RowNumber;

            // 重名检测：与"目标租户同租户"或 host 全局下的同名用户算作冲突
            userNameToExistingUser.TryGetValue((NormalizeUserName(pr.Parsed!.UserName), pr.EffectiveTenantId), out var existingUser);
            if (existingUser != null)
            {
                if (overwriteExisting && !isPreview)
                {
                    try
                    {
                        await OverwriteIdentityUserAsync(existingUser, pr.Parsed!);
                        item.Status = UserImportItemStatus.Overwrite;
                        item.ExistingUserId = existingUser.Id.ToString();
                        result.OverwriteCount++;
                    }
                    catch (Exception ex)
                    {
                        item.Status = UserImportItemStatus.Fail;
                        item.Reason = $"覆盖失败：{ex.Message}";
                        result.FailCount++;
                    }
                }
                else if (overwriteExisting && isPreview)
                {
                    // 预览阶段：标记 Overwrite 让用户确认
                    item.Status = UserImportItemStatus.Overwrite;
                    item.ExistingUserId = existingUser.Id.ToString();
                    item.Reason = "已存在同名用户，导入时将覆盖";
                    result.OverwriteCount++;
                }
                else
                {
                    item.Status = UserImportItemStatus.Skip;
                    item.Reason = $"已存在同名用户（{existingUser.UserName}），未开启覆盖，已跳过";
                    item.ExistingUserId = existingUser.Id.ToString();
                    result.SkipCount++;
                }
                AddItem(result, item);
                continue;
            }

            // 新建
            if (isPreview)
            {
                item.Status = UserImportItemStatus.New;
                result.NewCount++;
            }
            else
            {
                try
                {
                    await CreateIdentityUserAsync(pr.Parsed!, pr.EffectiveTenantId);
                    item.Status = UserImportItemStatus.New;
                    result.NewCount++;
                }
                catch (Exception ex)
                {
                    item.Status = UserImportItemStatus.Fail;
                    item.Reason = ex.Message;
                    result.FailCount++;
                }
            }
            AddItem(result, item);
        }

        result.TotalCount = result.Items.Count;
        result.FailItems = result.Items
            .Where(i => i.Status == UserImportItemStatus.Fail)
            .Select(i => new UserImportFailItemDto
            {
                RowNumber = i.RowNumber,
                UserName = i.UserName,
                Reason = i.Reason ?? string.Empty,
            })
            .ToList();
        return result;
    }

    private static void AddItem(UserImportResultDto result, UserImportPreviewItemDto item)
    {
        result.Items.Add(item);
        if (item.Status == UserImportItemStatus.Fail)
        {
            result.FailCount++;
        }
    }

    /// <summary>
    /// 一次性查回所有相关用户，按 (NormalizedUserName, TenantId) 索引；
    /// TenantId=null 表示 host 全局用户。
    /// </summary>
    private async Task<Dictionary<(string UserName, Guid? TenantId), Volo.Abp.Identity.IdentityUser>> GetExistingUserLookupAsync(
        List<ParsedImportRow> parsedRows)
    {
        // 收集需要查询的 userName 与目标 TenantId
        var userNames = parsedRows
            .Where(r => r.ParseError == null && r.Parsed != null && !string.IsNullOrWhiteSpace(r.Parsed.UserName))
            .Select(r => NormalizeUserName(r.Parsed!.UserName))
            .Distinct()
            .ToList();

        var lookup = new Dictionary<(string, Guid?), Volo.Abp.Identity.IdentityUser>();
        if (userNames.Count == 0)
        {
            return lookup;
        }

        // 用 DataFilter.Disable<IMultiTenant> 同时查询 host 与所有租户的用户；
        // 用 IQueryable 按 NormalizedUserName 做大小写不敏感匹配，避免一个 userName 一次查询的 N+1。
        using (DataFilter.Disable<IMultiTenant>())
        {
            var queryable = await _userRepository.GetQueryableAsync();
            var matches = await AsyncExecuter.ToListAsync(
                queryable.Where(u => userNames.Contains(u.NormalizedUserName))
            );
            foreach (var u in matches)
            {
                lookup[(u.NormalizedUserName ?? string.Empty, u.TenantId)] = u;
            }
        }
        return lookup;
    }

    private static string NormalizeUserName(string userName)
    {
        // ABP Identity 默认 UpperInvariant 规范化；为了匹配数据库，这里也按同样规则归一化
        return userName.Trim().ToUpperInvariant();
    }

    // ────────────────────── 单行解析 ──────────────────────

    /// <summary>
    /// 解析单行：先做基础字段读取，再做通用必填校验。
    /// 返回 null 表示该行全空，应跳过；否则返回 ParsedImportRow（含可能的 ParseError）。
    /// </summary>
    private ParsedImportRow? ParseSingleRow(IXLRangeRow row, int headerRowNumber)
    {
        var rowNumber = row.RowNumber();
        if (IsEmptyRow(row))
        {
            return null; // 静默跳过空行
        }

        try
        {
            var roleTypeStr = row.Cell(1).GetString().Trim();
            if (!RoleDisplayNameMapping.TryGetValue(roleTypeStr, out var roleType))
            {
                return new ParsedImportRow
                {
                    RowNumber = rowNumber,
                    ParseError = $"第 {rowNumber} 行：角色类型「{roleTypeStr}」无效，应为：联盟管理员/院校管理员/教师/学生/企业用户",
                };
            }

            var dto = new UserImportDto
            {
                RoleType = roleType,
                Name = row.Cell(2).GetString().Trim(),
                UserName = row.Cell(3).GetString().Trim(),
                Password = row.Cell(4).GetString(), // 保留原始字符串，不 Trim：密码可能含空格
                PhoneNumber = row.Cell(5).GetString().Trim(),
                Email = row.Cell(6).GetString().Trim(),
                EmployeeNumber = row.Cell(8).GetString().Trim(),
                Department = row.Cell(9).GetString().Trim(),
                Major = row.Cell(10).GetString().Trim(),
                Course = row.Cell(11).GetString().Trim(),
                Title = row.Cell(12).GetString().Trim(),
                StudentNumber = row.Cell(13).GetString().Trim(),
                Grade = row.Cell(14).GetString().Trim(),
                ClassName = row.Cell(15).GetString().Trim(),
                ManagementScope = row.Cell(16).GetString().Trim(),
                CompanyName = row.Cell(17).GetString().Trim(),
                Position = row.Cell(18).GetString().Trim(),
                Industry = row.Cell(19).GetString().Trim(),
                PartnerSchool = row.Cell(20).GetString().Trim(),
                Remark = row.Cell(21).GetString().Trim(),
                TenantName = string.IsNullOrWhiteSpace(row.Cell(22).GetString()) ? null : row.Cell(22).GetString().Trim(),
            };

            // 通用必填校验（跨所有角色）
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return new ParsedImportRow { RowNumber = rowNumber, ParseError = "姓名为必填项" };
            }
            if (string.IsNullOrWhiteSpace(dto.UserName))
            {
                return new ParsedImportRow { RowNumber = rowNumber, ParseError = "登录账号为必填项" };
            }
            if (string.IsNullOrWhiteSpace(dto.Password))
            {
                return new ParsedImportRow { RowNumber = rowNumber, ParseError = "初始密码为必填项" };
            }
            if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
            {
                return new ParsedImportRow { RowNumber = rowNumber, ParseError = "手机号为必填项" };
            }

            return new ParsedImportRow
            {
                RowNumber = rowNumber,
                RoleType = roleType,
                Parsed = dto,
            };
        }
        catch (Exception ex)
        {
            return new ParsedImportRow
            {
                RowNumber = rowNumber,
                ParseError = $"第 {rowNumber} 行解析失败：{ex.Message}",
            };
        }
    }

    /// <summary>
    /// 把 ParsedImportRow.TenantName 解析为 EffectiveTenantId。
    /// - 租户管理员：忽略输入的 TenantName，强制使用 _currentTenant.Id。
    /// - host 管理员：
    ///   - 显式传入了 TenantId → 优先用；
    ///   - 否则按行内 TenantName 解析；解析不到则该行标记失败。
    ///   - 留空且角色为全局（联盟管理员/企业用户）→ TenantId=null。
    /// </summary>
    private async Task ResolveTenantsAsync(List<ParsedImportRow> rows, Guid? explicitTenantId)
    {
        var isHost = !_currentTenant.Id.HasValue;

        // 收集所有非空 TenantName，一次性查询缓存
        var neededNames = rows
            .Where(r => r.Parsed != null && !string.IsNullOrWhiteSpace(r.Parsed.TenantName))
            .Select(r => r.Parsed!.TenantName!.Trim())
            .Distinct()
            .ToList();

        Dictionary<string, Guid> tenantNameToId = new(StringComparer.OrdinalIgnoreCase);
        if (isHost && neededNames.Count > 0)
        {
            var allTenants = await _tenantRepository.GetListAsync(includeDetails: false);
            tenantNameToId = allTenants
                .Where(t => !string.IsNullOrWhiteSpace(t.Name))
                .GroupBy(t => t.Name!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Id);
        }

        foreach (var pr in rows)
        {
            if (pr.Parsed == null) continue;

            if (!isHost)
            {
                pr.EffectiveTenantId = _currentTenant.Id;
                continue;
            }

            // host 管理员
            if (explicitTenantId.HasValue)
            {
                pr.EffectiveTenantId = explicitTenantId.Value;
                pr.Parsed.TenantName = allTenantsNameFor(explicitTenantId.Value, tenantNameToId);
                continue;
            }

            if (string.IsNullOrWhiteSpace(pr.Parsed.TenantName))
            {
                // 留空：仅全局角色（联盟管理员/企业用户）合法
                if (pr.Parsed.RoleType == UserRoleType.LeagueAdmin || pr.Parsed.RoleType == UserRoleType.EnterpriseUser)
                {
                    pr.EffectiveTenantId = null;
                }
                else
                {
                    pr.ParseError = pr.ParseError ?? $"角色「{pr.Parsed.RoleType}」必须填写归属租户";
                }
            }
            else if (tenantNameToId.TryGetValue(pr.Parsed.TenantName!, out var tid))
            {
                pr.EffectiveTenantId = tid;
            }
            else
            {
                pr.ParseError = pr.ParseError ?? $"租户名称「{pr.Parsed.TenantName}」不存在";
            }
        }
    }

    private static string? allTenantsNameFor(Guid tenantId, Dictionary<string, Guid> nameToId)
    {
        foreach (var kv in nameToId)
        {
            if (kv.Value == tenantId) return kv.Key;
        }
        return null;
    }

    /// <summary>
    /// 业务级校验：角色专属必填项 + 租户范围限制。
    /// </summary>
    private static string? ValidateBusinessRules(ParsedImportRow pr)
    {
        if (pr.Parsed == null) return null;

        var roleType = pr.RoleType;
        var required = RequiredFieldsMapping[roleType];
        var dto = pr.Parsed;

        foreach (var field in required)
        {
            var value = field switch
            {
                "工号" => dto.EmployeeNumber,
                "所属院系/部门" => dto.Department,
                "专业" => dto.Major,
                "学号" => dto.StudentNumber,
                "年级" => dto.Grade,
                "班级" => dto.ClassName,
                "邮箱" => dto.Email,
                "企业名称" => dto.CompanyName,
                "职位/岗位" => dto.Position,
                _ => null,
            };
            if (string.IsNullOrWhiteSpace(value))
            {
                return $"「{field}」为必填项（{RoleTypeDisplayNames.GetValueOrDefault(roleType, roleType.ToString())}）";
            }
        }

        // 邮箱格式（仅当非空时校验；空已经在上面拦过了）
        if (!string.IsNullOrWhiteSpace(dto.Email) && !IsValidEmail(dto.Email))
        {
            return $"邮箱格式不合法：{dto.Email}";
        }

        // 手机号格式（最宽松校验：11 位数字）
        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber) && !System.Text.RegularExpressions.Regex.IsMatch(dto.PhoneNumber, @"^\d{11}$"))
        {
            return $"手机号格式不合法：{dto.PhoneNumber}（应为 11 位数字）";
        }

        // LeagueAdmin 仅允许全局（TenantId=null）
        if (roleType == UserRoleType.LeagueAdmin && pr.EffectiveTenantId.HasValue)
        {
            return "联盟管理员为全局角色，不允许分配到具体租户";
        }

        // EnterpriseUser 全局允许；如分配到某租户，需租户已建立 EnterpriseUser 角色
        // 实际上创建时若找不到租户级角色，ResolveRoleForUserAsync 会回退到全局角色
        // —— 但导入时不希望这种"隐式回退"，先放过不拦。

        return null;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 在前 3 行内定位表头行：以 B 列=="姓名" 且 C 列=="登录账号" 为锚点。
    /// 新模板表头在第 3 行，旧模板表头在第 1 行；找不到时返回 1（保持旧行为）。
    /// </summary>
    private static int FindHeaderRowNumber(List<IXLRangeRow> rows)
    {
        foreach (var row in rows)
        {
            if (row.RowNumber() > 3) break;
            if (row.Cell(2).GetString().Trim() == "姓名"
                && row.Cell(3).GetString().Trim() == "登录账号")
            {
                return row.RowNumber();
            }
        }
        return 1;
    }

    /// <summary>判断是否为全空行（按 UserImportTemplateHeaders 实际列数）。</summary>
    private static bool IsEmptyRow(IXLRangeRow row)
    {
        for (var i = 1; i <= UserImportTemplateHeaders.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(row.Cell(i).GetString()))
            {
                return false;
            }
        }
        return true;
    }

    // ────────────────────── 用户创建 / 覆盖 ──────────────────────

    /// <summary>
    /// 新建用户。
    /// 修复历史 bug：必须把 <paramref name="tenantId"/> 传给 IdentityUser 构造函数，
    /// 否则租户管理员导入时会创建出 host 全局用户，回到列表看不到数据。
    /// </summary>
    private async Task CreateIdentityUserAsync(UserImportDto dto, Guid? tenantId)
    {
        // 学生专业的存在性校验必须在建用户之前：否则建用户成功后抛异常会导致残留半成品账号，
        // 重试时会报"用户名已存在"。
        Guid? studentMajorId = null;
        if (dto.RoleType == UserRoleType.Student && !string.IsNullOrWhiteSpace(dto.Major))
        {
            var resolved = await _majorRepository.FindByNameAsync(dto.Major);
            if (resolved == null)
            {
                throw new UserFriendlyException($"专业【{dto.Major}】不存在，请先在专业管理中创建");
            }
            studentMajorId = resolved.Id;
        }

        // 邮箱为空时用默认邮箱兜底（Excel 空单元格读出来是 "" 而非 null）。
        var email = string.IsNullOrWhiteSpace(dto.Email)
            ? $"{dto.UserName}@default.com"
            : dto.Email.Trim();
        var user = new Volo.Abp.Identity.IdentityUser(
            GuidGenerator.Create(),
            dto.UserName,
            email,
            tenantId: tenantId  // ★ 修复：必须传 TenantId，否则租户管理员导入的用户会变成全局用户
        )
        {
            Name = dto.Name
        };

        user.SetPhoneNumber(dto.PhoneNumber, false);

        var createResult = await _identityUserManager.CreateAsync(user, dto.Password, false);
        if (!createResult.Succeeded)
        {
            var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
            throw new UserFriendlyException($"创建用户失败: {errors}");
        }

        ApplyExtraProperties(user, dto, studentMajorId);

        var roleName = GetRoleNameByRoleType(dto.RoleType);
        var role = await ResolveRoleForUserAsync(roleName, user.TenantId);
        if (role != null)
        {
            user.AddRole(role.Id);
        }

        await _identityUserRepository.UpdateAsync(user);
    }

    /// <summary>
    /// 覆盖现有用户：仅更新姓名/手机号/邮箱/扩展属性，不修改登录账号与密码。
    /// 角色：保留原有角色并附加新角色（按角色类型解析），不去掉已有角色，
    /// 避免误删管理员手动配置的特殊权限。
    /// </summary>
    private async Task OverwriteIdentityUserAsync(Volo.Abp.Identity.IdentityUser existing, UserImportDto dto)
    {
        // 学生专业预解析
        Guid? studentMajorId = null;
        if (dto.RoleType == UserRoleType.Student && !string.IsNullOrWhiteSpace(dto.Major))
        {
            var resolved = await _majorRepository.FindByNameAsync(dto.Major);
            if (resolved == null)
            {
                throw new UserFriendlyException($"专业【{dto.Major}】不存在，请先在专业管理中创建");
            }
            studentMajorId = resolved.Id;
        }

        // 邮箱更新（如提供）
        if (!string.IsNullOrWhiteSpace(dto.Email) && !string.Equals(existing.Email, dto.Email.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            (await _identityUserManager.SetEmailAsync(existing, dto.Email.Trim())).CheckErrors();
        }

        // 姓名
        existing.Name = dto.Name;

        // 手机号
        existing.SetPhoneNumber(dto.PhoneNumber, false);

        // 扩展属性整体覆盖（按字段写入，未提供的字段保留原值）
        ApplyExtraProperties(existing, dto, studentMajorId);

        // 角色：若用户当前没有对应角色，则补一个
        var roleName = GetRoleNameByRoleType(dto.RoleType);
        var role = await ResolveRoleForUserAsync(roleName, existing.TenantId);
        if (role != null)
        {
            using (_currentTenant.Change(existing.TenantId))
            {
                var currentRoles = await _identityUserManager.GetRolesAsync(existing);
                if (!currentRoles.Contains(role.Name, StringComparer.OrdinalIgnoreCase))
                {
                    existing.AddRole(role.Id);
                }
            }
        }

        (await _identityUserManager.UpdateAsync(existing)).CheckErrors();
    }

    private static void ApplyExtraProperties(Volo.Abp.Identity.IdentityUser user, UserImportDto dto, Guid? studentMajorId)
    {
        user.ExtraProperties["RoleType"] = (int)dto.RoleType;

        if (!string.IsNullOrWhiteSpace(dto.SchoolId))
            user.ExtraProperties["SchoolId"] = dto.SchoolId;

        if (!string.IsNullOrWhiteSpace(dto.EmployeeNumber))
            user.ExtraProperties["EmployeeNumber"] = dto.EmployeeNumber;

        if (!string.IsNullOrWhiteSpace(dto.Department))
            user.ExtraProperties["Department"] = dto.Department;

        if (studentMajorId.HasValue)
        {
            user.ExtraProperties[MajorIdExtraProperty] = studentMajorId.Value;
        }
        else if (!string.IsNullOrWhiteSpace(dto.Major))
        {
            user.ExtraProperties["Major"] = dto.Major;
        }

        if (!string.IsNullOrWhiteSpace(dto.Course))
            user.ExtraProperties["Course"] = dto.Course;
        if (!string.IsNullOrWhiteSpace(dto.Title))
            user.ExtraProperties["Title"] = dto.Title;
        if (!string.IsNullOrWhiteSpace(dto.StudentNumber))
            user.ExtraProperties["StudentNumber"] = dto.StudentNumber;
        if (!string.IsNullOrWhiteSpace(dto.Grade))
            user.ExtraProperties["Grade"] = dto.Grade;
        if (!string.IsNullOrWhiteSpace(dto.ClassName))
            user.ExtraProperties["ClassName"] = dto.ClassName;
        if (!string.IsNullOrWhiteSpace(dto.ManagementScope))
            user.ExtraProperties["ManagementScope"] = dto.ManagementScope;
        if (!string.IsNullOrWhiteSpace(dto.CompanyName))
            user.ExtraProperties["CompanyName"] = dto.CompanyName;
        if (!string.IsNullOrWhiteSpace(dto.UnifiedSocialCreditCode))
            user.ExtraProperties["UnifiedSocialCreditCode"] = dto.UnifiedSocialCreditCode;
        if (!string.IsNullOrWhiteSpace(dto.Position))
            user.ExtraProperties["Position"] = dto.Position;
        if (!string.IsNullOrWhiteSpace(dto.Industry))
            user.ExtraProperties["Industry"] = dto.Industry;
        if (!string.IsNullOrWhiteSpace(dto.PartnerSchool))
            user.ExtraProperties["PartnerSchool"] = dto.PartnerSchool;
        if (!string.IsNullOrWhiteSpace(dto.Remark))
            user.ExtraProperties["Remark"] = dto.Remark;
    }

    /// <summary>
    /// 为导入用户解析角色：优先取与用户同租户（TenantId）的角色；
    /// 只有宿主用户才允许分配到宿主级角色。
    /// </summary>
    private async Task<Volo.Abp.Identity.IdentityRole?> ResolveRoleForUserAsync(string roleName, Guid? userTenantId)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var roles = await _identityRoleRepository.GetListAsync(includeDetails: false);
            var normalizedName = roleName.ToUpperInvariant();
            var candidates = roles
                .Where(r => r.NormalizedName == normalizedName)
                .ToList();

            // 优先同租户角色；宿主用户回退到宿主级角色；租户用户找不到租户角色时返回 null（不误配宿主角色）
            var sameTenantRole = candidates.FirstOrDefault(r => r.TenantId == userTenantId);
            if (sameTenantRole != null)
            {
                return sameTenantRole;
            }

            return userTenantId == null
                ? candidates.FirstOrDefault(r => r.TenantId == null)
                : null;
        }
    }

    private static string GetRoleNameByRoleType(UserRoleType roleType)
    {
        return roleType switch
        {
            UserRoleType.LeagueAdmin => "LeagueAdmin",
            UserRoleType.SchoolAdmin => "SchoolAdmin",
            UserRoleType.Teacher => "Teacher",
            UserRoleType.Student => "Student",
            UserRoleType.EnterpriseUser => "EnterpriseUser",
            _ => "User",
        };
    }

    // ────────────────────── 角色权限概要 / 模板下载 ──────────────────────

    private static readonly Dictionary<string, (string DisplayName, bool IsGlobal)> RoleMeta = new()
    {
        ["LeagueAdmin"] = ("联盟管理员", true),
        ["SchoolAdmin"] = ("院校管理员", false),
        ["Teacher"] = ("教师", false),
        ["Student"] = ("学生", false),
        ["EnterpriseUser"] = ("企业用户", true),
        ["admin"] = ("平台管理员", true),
    };

    private static readonly HashSet<string> LeagueExclusivePermissions = new()
    {
        KnowledgeHubPermissions.Resources.LeagueAudit,
        KnowledgeHubPermissions.Resources.PhysicalDelete,
        KnowledgeHubPermissions.RecruitmentLive.Manage,
    };

    /// <summary>
    /// P1-2：返回所有预置角色的「权限概要」，让管理员确认 SchoolAdmin 与 LeagueAdmin
    /// 实际差异。统计走 IPermissionManager.GetAllAsync → GrantedPermissions，
    /// 不返回权限列表本身（避免泄漏内部命名），仅返回数量 + 关键权限标记。
    /// </summary>
    [Authorize(KnowledgeHubPermissions.Users.Default)]
    public async Task<List<RolePermissionSummaryDto>> GetRolePermissionSummaryAsync()
    {
        var result = new List<RolePermissionSummaryDto>();
        foreach (var (roleName, (displayName, isGlobal)) in RoleMeta)
        {
            var allPerms = await _permissionManager.GetAllAsync("R", roleName);
            var granted = allPerms
                .Where(p => p.Providers != null && p.Providers.Count > 0)
                .ToList();

            var highlights = granted
                .Where(p => LeagueExclusivePermissions.Contains(p.Name))
                .Select(p => p.Name)
                .ToList();

            result.Add(new RolePermissionSummaryDto
            {
                RoleName = roleName,
                DisplayName = displayName,
                IsGlobal = isGlobal,
                GrantedPermissionCount = granted.Count,
                HighlightPermissions = highlights,
            });
        }
        return result;
    }

    /// <summary>
    /// 生成用户批量导入 Excel 模板：单 Sheet 包含全部角色类型，第 1 列"角色类型"区分必填项；
    /// 第 1 行标题、第 2 行说明、第 3 行表头（附列批注），第 4 行起为数据区；
    /// 必填列高亮、「角色类型」提供下拉列表。
    /// </summary>
    [Authorize(KnowledgeHubPermissions.Users.Import)]
    public Task<IRemoteStreamContent> GetImportTemplateAsync()
    {
        using var workbook = new XLWorkbook();
        BuildImportSheet(workbook);
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        return Task.FromResult<IRemoteStreamContent>(
            new RemoteStreamContent(
                stream,
                $"用户导入模板_{Clock.Now:yyyyMMdd}.xlsx",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
    }

    /// <summary>
    /// 构建单 Sheet 的导入模板：
    ///   第 1 行：标题（合并）
    ///   第 2 行：使用说明 + 必填项提示（合并）
    ///   第 3 行：表头（必填列高亮，并附列批注说明）
    ///   第 4 行起：可填写的数据区（「角色类型」列提供下拉列表校验）
    /// 工作表受保护：标题/说明/表头行锁定，数据列可编辑。
    /// </summary>
    private static void BuildImportSheet(XLWorkbook workbook)
    {
        var worksheet = workbook.Worksheets.Add(DefaultSheetName);

        // 第 1 行：标题
        worksheet.Cell(1, 1).Value = "用户批量导入模板";
        worksheet.Range(1, 1, 1, UserImportTemplateHeaders.Length).Merge();
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;
        worksheet.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromArgb(30, 108, 232);
        worksheet.Cell(1, 1).Style.Font.FontColor = XLColor.White;
        worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Cell(1, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        worksheet.Row(1).Height = 28;

        // 第 2 行：使用说明（合并 + 灰色斜体 + 自动换行）
        var notes = new[]
        {
            "1. 所有用户填在同一张 Sheet 内，第 1 列「角色类型」请通过下拉列表选择（联盟管理员/院校管理员/教师/学生/企业用户）。",
            "2. 通用必填：姓名、登录账号、初始密码、手机号；其它必填项随角色不同，把鼠标移到表头单元格即可查看列批注说明（橙黄色表头为必填列）。",
            "3. 数据请从第 4 行开始逐行填写；标题、说明与表头行已锁定，不会参与导入。",
            "4. 学生的「专业」按名称解析为系统内专业；教师「专业」按字符串保存。",
            "5. 「租户名称」仅系统（host）管理员使用：留空表示导入全局用户，填写则导入到对应租户；租户管理员导入时无需填写，该列会被忽略，用户直接归属本租户。",
            "6. 登录账号不可重复；若已存在同名用户，导入时默认跳过，开启「覆盖」后会更新其姓名/手机号/邮箱等信息（不改密码）。",
        };
        worksheet.Cell(2, 1).Value = string.Join("\n", notes);
        worksheet.Range(2, 1, 2, UserImportTemplateHeaders.Length).Merge();
        worksheet.Cell(2, 1).Style.Font.Italic = true;
        worksheet.Cell(2, 1).Style.Font.FontColor = XLColor.Gray;
        worksheet.Cell(2, 1).Style.Alignment.WrapText = true;
        worksheet.Cell(2, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        worksheet.Row(2).Height = 96;

        // 第 3 行：表头
        // 通用必填列：角色类型/姓名/登录账号/初始密码/手机号
        var universalRequired = new HashSet<string> { "角色类型", "姓名", "登录账号", "初始密码", "手机号" };
        // 角色专属必填：把所有 RequiredFieldsMapping 的字段打平
        var roleSpecificRequired = new HashSet<string>();
        foreach (var (_, fields) in RequiredFieldsMapping)
        {
            foreach (var f in fields) roleSpecificRequired.Add(f);
        }

        for (var i = 0; i < UserImportTemplateHeaders.Length; i++)
        {
            var header = UserImportTemplateHeaders[i];
            var cell = worksheet.Cell(3, i + 1);
            cell.Value = header;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = (universalRequired.Contains(header) || roleSpecificRequired.Contains(header))
                ? RequiredHeaderColor
                : OptionalHeaderColor;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            // 用批注说明每列的填写要求，避免在模板里堆砌示例数据。
            var hint = GetHeaderHint(header);
            if (hint != null)
            {
                cell.CreateComment().AddText(hint);
            }
        }
        worksheet.Row(3).Height = 22;

        // 第 1 列「角色类型」下拉列表：数据行只能从列表中选择，避免手填出错。
        var roleNames = string.Join(",", RoleDisplayNameMapping.Keys);
        var firstDataRow = 4;
        var roleRange = worksheet.Range(firstDataRow, 1, 1000, 1);
        roleRange.CreateDataValidation().List($"\"{roleNames}\"", true);

        // 保护工作表：数据列默认解锁（列级样式，避免逐行生成单元格），仅标题/说明/表头行锁定。
        for (var col = 1; col <= UserImportTemplateHeaders.Length; col++)
        {
            worksheet.Column(col).Style.Protection.SetLocked(false);
        }
        worksheet.Range(1, 1, firstDataRow - 1, UserImportTemplateHeaders.Length).Style.Protection.SetLocked(true);
        worksheet.Protect(XLSheetProtectionElements.SelectLockedCells | XLSheetProtectionElements.SelectUnlockedCells);

        // 冻结前 3 行；列宽按字段内容手工设定（前部关键字段留足宽度，避免显示不全）
        worksheet.SheetView.FreezeRows(3);
        ApplyColumnWidths(worksheet);
    }

    /// <summary>列宽设置：前部关键字段（角色类型/姓名/登录账号/初始密码/手机号/邮箱）留足宽度。</summary>
    private static void ApplyColumnWidths(IXLWorksheet worksheet)
    {
        var widths = new Dictionary<string, double>
        {
            { "角色类型", 14 },
            { "姓名", 14 },
            { "登录账号", 20 },
            { "初始密码", 16 },
            { "手机号", 16 },
            { "邮箱", 24 },
            { "所属院校", 20 },
            { "工号", 14 },
            { "所属院系/部门", 22 },
            { "专业", 24 },
            { "所教课程", 20 },
            { "职称", 12 },
            { "学号", 16 },
            { "年级", 10 },
            { "班级", 16 },
            { "管理范围", 16 },
            { "企业名称", 26 },
            { "职位/岗位", 16 },
            { "行业", 14 },
            { "合作学校", 20 },
            { "备注", 24 },
            { "租户名称", 20 },
        };

        for (var i = 0; i < UserImportTemplateHeaders.Length; i++)
        {
            var header = UserImportTemplateHeaders[i];
            worksheet.Column(i + 1).Width = widths.TryGetValue(header, out var width) ? width : 16;
        }
    }

    /// <summary>表头批注：说明该列的填写要求（必填角色等）。</summary>
    private static string? GetHeaderHint(string header)
    {
        return header switch
        {
            "角色类型" => "点击单元格右侧的下拉箭头选择：联盟管理员 / 院校管理员 / 教师 / 学生 / 企业用户。",
            "姓名" => "必填。用户真实姓名。",
            "登录账号" => "必填。登录用户名，系统内不可重复。",
            "初始密码" => "必填。首次登录密码，建议用户登录后自行修改。",
            "手机号" => "必填。11 位手机号。",
            "邮箱" => "选填；企业用户必填。",
            "所属院校" => "选填。用户所属院校名称。",
            "工号" => "联盟管理员 / 院校管理员 / 教师 必填。",
            "所属院系/部门" => "教师必填。",
            "专业" => "教师 / 学生 必填。学生按专业名称匹配系统内已有专业。",
            "所教课程" => "选填。仅教师。",
            "职称" => "选填。仅教师。",
            "学号" => "学生必填。",
            "年级" => "学生必填。",
            "班级" => "学生必填。",
            "管理范围" => "选填。仅院校管理员。",
            "企业名称" => "企业用户必填。",
            "职位/岗位" => "企业用户必填。",
            "行业" => "选填。仅企业用户。",
            "合作学校" => "选填。",
            "备注" => "选填。",
            "租户名称" => "仅系统（host）管理员使用。留空表示导入为全局用户；租户管理员导入时忽略此列，用户直接归属本租户。",
            _ => null,
        };
    }

    // ────────────────────── 内部数据结构 ──────────────────────

    /// <summary>
    /// 解析阶段的中间结果：包含原始 RowNumber、可能失败的 ParseError，
    /// 以及成功时填充的 Parsed（DTO）和 EffectiveTenantId（用于后续校验 / 落库 / 重名检测）。
    /// </summary>
    private class ParsedImportRow
    {
        public int RowNumber { get; set; }
        public UserRoleType RoleType { get; set; }
        public UserImportDto? Parsed { get; set; }
        public string? ParseError { get; set; }
        public Guid? EffectiveTenantId { get; set; }
    }
}
