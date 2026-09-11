using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using KnowledgeHub.Majors;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.Data;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Uow;

namespace KnowledgeHub.Users;

[Authorize(KnowledgeHubPermissions.Users.Import)]
public class UserImportAppService : KnowledgeHubAppService, IUserImportAppService
{
    private const string MajorIdExtraProperty = "MajorId";

    private readonly IIdentityUserRepository _identityUserRepository;
    private readonly IdentityUserManager _identityUserManager;
    private readonly IIdentityRoleRepository _identityRoleRepository;
    private readonly IMajorRepository _majorRepository;
    private readonly IPermissionManager _permissionManager;

    private static readonly Dictionary<string, UserRoleType> SheetRoleMapping = new()
    {
        { "联盟管理员", UserRoleType.LeagueAdmin },
        { "院校管理员", UserRoleType.SchoolAdmin },
        { "教师", UserRoleType.Teacher },
        { "学生", UserRoleType.Student },
        { "企业用户", UserRoleType.EnterpriseUser }
    };

    private static readonly Dictionary<UserRoleType, List<string>> RequiredFieldsMapping = new()
    {
        { UserRoleType.LeagueAdmin, new List<string> { "角色类型", "姓名", "登录账号", "初始密码", "手机号", "工号" } },
        { UserRoleType.SchoolAdmin, new List<string> { "角色类型", "姓名", "登录账号", "初始密码", "手机号", "所属院校", "工号" } },
        { UserRoleType.Teacher, new List<string> { "角色类型", "姓名", "登录账号", "初始密码", "手机号", "所属院校", "工号", "所属院系/部门", "所教专业" } },
        { UserRoleType.Student, new List<string> { "角色类型", "姓名", "登录账号", "初始密码", "手机号", "所属院校", "专业", "学号", "年级", "班级" } },
        { UserRoleType.EnterpriseUser, new List<string> { "角色类型", "姓名", "登录账号", "初始密码", "手机号", "邮箱", "企业名称", "统一社会信用代码", "职位/岗位" } }
    };

    /// <summary>
    /// 用户导入模板表头（22 列），与 ParseRow 读取的列顺序严格对应。
    /// </summary>
    private static readonly string[] UserImportTemplateHeaders =
    {
        "角色类型", "姓名", "登录账号", "初始密码", "手机号", "邮箱",
        "所属院校", "工号", "所属院系/部门", "专业", "所教课程", "职称",
        "学号", "年级", "班级", "管理范围", "企业名称", "统一社会信用代码",
        "职位/岗位", "行业", "合作学校", "备注"
    };

    /// <summary>
    /// 必填列背景色（用于在模板表头中高亮必填项）。
    /// </summary>
    private static readonly XLColor RequiredHeaderColor = XLColor.FromArgb(255, 244, 230);

    /// <summary>
    /// 可选列表头背景色。
    /// </summary>
    private static readonly XLColor OptionalHeaderColor = XLColor.FromArgb(232, 244, 255);

    public UserImportAppService(
        IIdentityUserRepository identityUserRepository,
        IdentityUserManager identityUserManager,
        IIdentityRoleRepository identityRoleRepository,
        IMajorRepository majorRepository,
        IPermissionManager permissionManager)
    {
        _identityUserRepository = identityUserRepository;
        _identityUserManager = identityUserManager;
        _identityRoleRepository = identityRoleRepository;
        _majorRepository = majorRepository;
        _permissionManager = permissionManager;
    }

    [UnitOfWork]
    public async Task<UserImportResultDto> ImportAsync(ImportUsersFileDto input)
    {
        var result = new UserImportResultDto();

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

        using var stream = new System.IO.MemoryStream(excelFile);
        using var workbook = new XLWorkbook(stream);

        foreach (var sheetName in SheetRoleMapping.Keys)
        {
            var worksheet = workbook.Worksheet(sheetName);
            if (worksheet == null) continue;

            var roleType = SheetRoleMapping[sheetName];
            var allRows = worksheet.RangeUsed()?.RowsUsed().ToList();
            if (allRows == null || allRows.Count == 0) continue;

            // 兼容新模板（第 1 行标题、第 2 行说明、第 3 行表头）与旧模板（第 1 行即表头）：
            // 定位表头行，数据从表头下一行开始；找不到则默认跳过第 1 行（旧行为）。
            var headerRowNumber = FindHeaderRowNumber(worksheet, allRows);
            var dataRows = allRows.Where(r => r.RowNumber() > headerRowNumber).ToList();
            if (dataRows.Count == 0) continue;

            var rowNumber = headerRowNumber + 1;
            var countedRows = 0;
            foreach (var row in dataRows)
            {
                // 全空行静默跳过（不计入总数），避免尾部空行产生误报。
                if (IsEmptyRow(row))
                {
                    rowNumber++;
                    continue;
                }
                countedRows++;

                try
                {
                    var userImportDto = ParseRow(row, roleType, rowNumber);
                    if (userImportDto == null)
                    {
                        result.FailCount++;
                        result.FailItems.Add(new UserImportFailItemDto
                        {
                            RowNumber = rowNumber,
                            UserName = "",
                            Reason = "数据解析失败"
                        });
                        rowNumber++;
                        continue;
                    }

                    var validationError = ValidateUserImport(userImportDto, roleType);
                    if (validationError != null)
                    {
                        result.FailCount++;
                        result.FailItems.Add(new UserImportFailItemDto
                        {
                            RowNumber = rowNumber,
                            UserName = userImportDto.UserName,
                            Reason = validationError
                        });
                        rowNumber++;
                        continue;
                    }

                    await CreateIdentityUserAsync(userImportDto);
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    result.FailCount++;
                    result.FailItems.Add(new UserImportFailItemDto
                    {
                        RowNumber = rowNumber,
                        UserName = "",
                        Reason = ex.Message
                    });
                }
                rowNumber++;
            }
            result.TotalCount += countedRows;
        }

        return result;
    }

    /// <summary>
    /// 在前 3 行内定位表头行：以 B 列=="姓名" 且 C 列=="登录账号" 为锚点。
    /// 新模板表头在第 3 行，旧模板表头在第 1 行；找不到时返回 1（保持旧行为）。
    /// </summary>
    private static int FindHeaderRowNumber(IXLWorksheet worksheet, List<IXLRangeRow> rows)
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

    /// <summary>判断是否为全空行（22 个模板列均为空）。</summary>
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

    private UserImportDto? ParseRow(IXLRangeRow row, UserRoleType roleType, int rowNumber)
    {
        try
        {
            // 学生表里"专业"在 J 列；教师表里 J 列是"所教专业"（保留为字符串 ExtraProperty）。
            // 这里统一从 J 列读取，CreateIdentityUserAsync 里再按角色处理。
            var dto = new UserImportDto
            {
                RoleType = roleType,
                Name = row.Cell("B").GetString(),
                UserName = row.Cell("C").GetString(),
                Password = row.Cell("D").GetString(),
                PhoneNumber = row.Cell("E").GetString(),
                Email = row.Cell("F").GetString(),
                EmployeeNumber = row.Cell("H").GetString(),
                Department = row.Cell("I").GetString(),
                Major = row.Cell("J").GetString(),
                Course = row.Cell("K").GetString(),
                Title = row.Cell("L").GetString(),
                StudentNumber = row.Cell("M").GetString(),
                Grade = row.Cell("N").GetString(),
                ClassName = row.Cell("O").GetString(),
                ManagementScope = row.Cell("P").GetString(),
                CompanyName = row.Cell("Q").GetString(),
                UnifiedSocialCreditCode = row.Cell("R").GetString(),
                Position = row.Cell("S").GetString(),
                Industry = row.Cell("T").GetString(),
                PartnerSchool = row.Cell("U").GetString(),
                Remark = row.Cell("V").GetString()
            };

            var schoolName = row.Cell("G").GetString();
            if (!string.IsNullOrWhiteSpace(schoolName))
            {
                // SchoolId will need to be resolved - placeholder for now
                dto.SchoolId = null;
            }

            return dto;
        }
        catch
        {
            return null;
        }
    }

    private string? ValidateUserImport(UserImportDto dto, UserRoleType roleType)
    {
        var requiredFields = RequiredFieldsMapping[roleType];

        if (string.IsNullOrWhiteSpace(dto.Name)) return "姓名为必填项";
        if (string.IsNullOrWhiteSpace(dto.UserName)) return "登录账号为必填项";
        if (string.IsNullOrWhiteSpace(dto.Password)) return "初始密码为必填项";
        if (string.IsNullOrWhiteSpace(dto.PhoneNumber)) return "手机号为必填项";

        switch (roleType)
        {
            case UserRoleType.LeagueAdmin:
                if (string.IsNullOrWhiteSpace(dto.EmployeeNumber)) return "工号为必填项";
                break;
            case UserRoleType.SchoolAdmin:
                if (string.IsNullOrWhiteSpace(dto.EmployeeNumber)) return "工号为必填项";
                break;
            case UserRoleType.Teacher:
                if (string.IsNullOrWhiteSpace(dto.EmployeeNumber)) return "工号为必填项";
                if (string.IsNullOrWhiteSpace(dto.Department)) return "所属院系/部门为必填项";
                if (string.IsNullOrWhiteSpace(dto.Major)) return "所教专业为必填项";
                break;
            case UserRoleType.Student:
                if (string.IsNullOrWhiteSpace(dto.StudentNumber)) return "学号为必填项";
                if (string.IsNullOrWhiteSpace(dto.Grade)) return "年级为必填项";
                if (string.IsNullOrWhiteSpace(dto.ClassName)) return "班级为必填项";
                if (string.IsNullOrWhiteSpace(dto.Major)) return "专业为必填项";
                break;
            case UserRoleType.EnterpriseUser:
                if (string.IsNullOrWhiteSpace(dto.Email)) return "邮箱为必填项";
                if (string.IsNullOrWhiteSpace(dto.CompanyName)) return "企业名称为必填项";
                if (string.IsNullOrWhiteSpace(dto.UnifiedSocialCreditCode)) return "统一社会信用代码为必填项";
                if (string.IsNullOrWhiteSpace(dto.Position)) return "职位/岗位为必填项";
                break;
        }

        return null;
    }

    private async Task CreateIdentityUserAsync(UserImportDto dto)
    {
        // 学生专业的存在性校验必须在建用户之前：否则建用户成功后抛异常会导致残留半成品账号，
        // 重试时会报“用户名已存在”。
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

        // 邮箱为空时用默认邮箱兜底（Excel 空单元格读出来是 "" 而非 null，?? 接不住）。
        var email = string.IsNullOrWhiteSpace(dto.Email)
            ? $"{dto.UserName}@default.com"
            : dto.Email.Trim();
        var user = new IdentityUser(
            GuidGenerator.Create(),
            dto.UserName,
            email
        )
        {
            // P1-7 修复：把导入表里的"姓名"写入 ABP IdentityUser.Name，
            // 否则就业/指导/投递等列表 GetUserDisplayName() 退化到 UserName/邮箱/GUID，
            // 教师/HR 看到的学生列全是 ID 截断，没有姓名。
            Name = dto.Name
        };

        user.SetPhoneNumber(dto.PhoneNumber, false);

        var createResult = await _identityUserManager.CreateAsync(
            user,
            dto.Password,
            false
        );

        if (!createResult.Succeeded)
        {
            var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
            throw new UserFriendlyException($"创建用户失败: {errors}");
        }

        user.ExtraProperties["RoleType"] = (int)dto.RoleType;

        if (!string.IsNullOrWhiteSpace(dto.SchoolId))
            user.ExtraProperties["SchoolId"] = dto.SchoolId;

        if (!string.IsNullOrWhiteSpace(dto.EmployeeNumber))
            user.ExtraProperties["EmployeeNumber"] = dto.EmployeeNumber;

        if (!string.IsNullOrWhiteSpace(dto.Department))
            user.ExtraProperties["Department"] = dto.Department;

        // 学生：把"专业"按名称解析为 MajorId 写入 ExtraProperties["MajorId"]
        // （存在性已在建用户前校验过，这里直接用预解析结果）。
        if (studentMajorId.HasValue)
        {
            user.ExtraProperties[MajorIdExtraProperty] = studentMajorId.Value;
        }
        else if (!string.IsNullOrWhiteSpace(dto.Major))
        {
            // 教师/其他角色：原"所教专业"等仍以字符串存放在 ExtraProperties.Major
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

        var roleName = GetRoleNameByRoleType(dto.RoleType);
        var role = await ResolveRoleForUserAsync(roleName, user.TenantId);
        if (role != null)
        {
            user.AddRole(role.Id);
        }

        await _identityUserRepository.UpdateAsync(user);
    }

    /// <summary>
    /// 为导入用户解析角色：优先取与用户同租户（TenantId）的角色；
    /// 只有宿主用户才允许分配到宿主级角色。
    /// 目的：避免历史 bug —— 租户用户被分配到宿主级同名角色
    /// （Student/Teacher/SchoolAdmin…），运行时角色解析为空导致行为异常。
    /// </summary>
    private async Task<IdentityRole?> ResolveRoleForUserAsync(string roleName, Guid? userTenantId)
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

    private string GetRoleNameByRoleType(UserRoleType roleType)
    {
        return roleType switch
        {
            UserRoleType.LeagueAdmin => "LeagueAdmin",
            UserRoleType.SchoolAdmin => "SchoolAdmin",
            UserRoleType.Teacher => "Teacher",
            UserRoleType.Student => "Student",
            UserRoleType.EnterpriseUser => "EnterpriseUser",
            _ => "User"
        };
    }

    // P1-2：角色 → 中文名 / 是否全局
    private static readonly Dictionary<string, (string DisplayName, bool IsGlobal)> RoleMeta = new()
    {
        ["LeagueAdmin"] = ("联盟管理员", true),
        ["SchoolAdmin"] = ("院校管理员", false),
        ["Teacher"] = ("教师", false),
        ["Student"] = ("学生", false),
        ["EnterpriseUser"] = ("企业用户", true),
        ["admin"] = ("平台管理员", true),
    };

    // P1-2：用于在前端突出显示「LeagueAdmin 独有」权限
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
            // IPermissionManager.GetAllAsync("R", roleName) 在 ABP 新版返回
            // List<PermissionWithGrantedProviders>，每条记录代表一个已声明的权限；
            // 若该权限被授予过指定 provider，则 Providers 列表会非空。
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
    /// 生成用户批量导入 Excel 模板：按角色类型分 Sheet（联盟管理员/院校管理员/教师/学生/企业用户），
    /// 每页含表头、说明、示例行；必填列表头用橙黄色高亮，便于管理员识别必填项。
    /// 模板与 ImportAsync 读取的列顺序严格对应。
    /// </summary>
    [Authorize(KnowledgeHubPermissions.Users.Import)]
    public Task<IRemoteStreamContent> GetImportTemplateAsync()
    {
        using var workbook = new XLWorkbook();

        foreach (var (sheetName, roleType) in SheetRoleMapping)
        {
            BuildImportSheet(workbook, sheetName, roleType);
        }

        // 首列页：使用说明总览
        BuildOverviewSheet(workbook);

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
    /// 构建单角色类型的导入模板 Sheet。
    /// 布局：
    ///   第 1 行：标题（合并）
    ///   第 2 行：使用说明 + 必填项提示（合并）
    ///   第 3 行：表头（必填列高亮）
    ///   第 4 行：示例（灰色斜体）
    /// </summary>
    private static void BuildImportSheet(XLWorkbook workbook, string sheetName, UserRoleType roleType)
    {
        var worksheet = workbook.Worksheets.Add(sheetName);
        var required = RequiredFieldsMapping[roleType];
        var requiredSet = new HashSet<string>(required);

        // 第 1 行：标题
        var titleText = $"{sheetName} - 用户导入模板";
        worksheet.Cell(1, 1).Value = titleText;
        worksheet.Range(1, 1, 1, UserImportTemplateHeaders.Length).Merge();
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;
        worksheet.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromArgb(30, 108, 232);
        worksheet.Cell(1, 1).Style.Font.FontColor = XLColor.White;
        worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Cell(1, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        worksheet.Row(1).Height = 28;

        // 第 2 行：使用说明
        var requiredText = string.Join("、", required);
        var note = $"必填项：{requiredText}。未填项可留空。示例行请删除后再上传。" +
                   $"导入后默认初始密码请用户首次登录后修改。";
        worksheet.Cell(2, 1).Value = note;
        worksheet.Range(2, 1, 2, UserImportTemplateHeaders.Length).Merge();
        worksheet.Cell(2, 1).Style.Font.Italic = true;
        worksheet.Cell(2, 1).Style.Font.FontColor = XLColor.Gray;
        worksheet.Cell(2, 1).Style.Alignment.WrapText = true;
        worksheet.Cell(2, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        worksheet.Row(2).Height = 36;

        // 第 3 行：表头
        for (var i = 0; i < UserImportTemplateHeaders.Length; i++)
        {
            var cell = worksheet.Cell(3, i + 1);
            cell.Value = UserImportTemplateHeaders[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = requiredSet.Contains(UserImportTemplateHeaders[i])
                ? RequiredHeaderColor
                : OptionalHeaderColor;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }
        worksheet.Row(3).Height = 22;

        // 第 4 行：示例（灰色斜体；不为必填项赋值，便于管理员删除整行）
        BuildSampleRow(worksheet, roleType);

        // 冻结前 3 行；列宽自适应
        worksheet.SheetView.FreezeRows(3);
        worksheet.Columns().AdjustToContents();
    }

    /// <summary>
    /// 填充示例行：仅填写必填项以便管理员看到格式。
    /// </summary>
    private static void BuildSampleRow(IXLWorksheet worksheet, UserRoleType roleType)
    {
        var sample = GetSampleRow(roleType);
        for (var i = 0; i < UserImportTemplateHeaders.Length; i++)
        {
            var cell = worksheet.Cell(4, i + 1);
            var header = UserImportTemplateHeaders[i];
            cell.Value = sample.TryGetValue(header, out var val) ? val : string.Empty;
            cell.Style.Font.FontColor = XLColor.Gray;
            cell.Style.Font.Italic = true;
        }
    }

    /// <summary>
    /// 各角色类型的示例数据：仅填写必填项以避免误导。
    /// </summary>
    private static Dictionary<string, string> GetSampleRow(UserRoleType roleType)
    {
        return roleType switch
        {
            UserRoleType.LeagueAdmin => new Dictionary<string, string>
            {
                { "角色类型", "联盟管理员" },
                { "姓名", "张三" },
                { "登录账号", "league_admin_demo" },
                { "初始密码", "Init@123" },
                { "手机号", "13800000000" },
                { "工号", "LA0001" },
            },
            UserRoleType.SchoolAdmin => new Dictionary<string, string>
            {
                { "角色类型", "院校管理员" },
                { "姓名", "李四" },
                { "登录账号", "school_admin_demo" },
                { "初始密码", "Init@123" },
                { "手机号", "13800000001" },
                { "所属院校", "示例学校" },
                { "工号", "SA0001" },
            },
            UserRoleType.Teacher => new Dictionary<string, string>
            {
                { "角色类型", "教师" },
                { "姓名", "王五" },
                { "登录账号", "teacher_demo" },
                { "初始密码", "Init@123" },
                { "手机号", "13800000002" },
                { "所属院校", "示例学校" },
                { "工号", "T0001" },
                { "所属院系/部门", "计算机学院" },
                { "专业", "计算机科学与技术" },
            },
            UserRoleType.Student => new Dictionary<string, string>
            {
                { "角色类型", "学生" },
                { "姓名", "赵六" },
                { "登录账号", "student_demo" },
                { "初始密码", "Init@123" },
                { "手机号", "13800000003" },
                { "所属院校", "示例学校" },
                { "专业", "计算机科学与技术" },
                { "学号", "2024001" },
                { "年级", "2024" },
                { "班级", "计科2401" },
            },
            UserRoleType.EnterpriseUser => new Dictionary<string, string>
            {
                { "角色类型", "企业用户" },
                { "姓名", "钱七" },
                { "登录账号", "enterprise_demo" },
                { "初始密码", "Init@123" },
                { "手机号", "13800000004" },
                { "邮箱", "hr@example.com" },
                { "企业名称", "示例科技有限公司" },
                { "统一社会信用代码", "91330000000000000X" },
                { "职位/岗位", "招聘经理" },
            },
            _ => new Dictionary<string, string>(),
        };
    }

    /// <summary>
    /// 构建使用说明 Sheet：列示所有角色类型、必填项、注意事项。
    /// </summary>
    private static void BuildOverviewSheet(XLWorkbook workbook)
    {
        var worksheet = workbook.Worksheets.Add("使用说明", 1);

        // 标题
        worksheet.Cell(1, 1).Value = "用户批量导入模板 · 使用说明";
        worksheet.Range(1, 1, 1, 2).Merge();
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;
        worksheet.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromArgb(30, 108, 232);
        worksheet.Cell(1, 1).Style.Font.FontColor = XLColor.White;
        worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Row(1).Height = 28;

        // 表头
        worksheet.Cell(3, 1).Value = "角色类型";
        worksheet.Cell(3, 2).Value = "必填字段";
        for (var c = 1; c <= 2; c++)
        {
            worksheet.Cell(3, c).Style.Font.Bold = true;
            worksheet.Cell(3, c).Style.Fill.BackgroundColor = OptionalHeaderColor;
            worksheet.Cell(3, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            worksheet.Cell(3, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        var row = 4;
        foreach (var (sheetName, roleType) in SheetRoleMapping)
        {
            worksheet.Cell(row, 1).Value = sheetName;
            worksheet.Cell(row, 2).Value = string.Join("、", RequiredFieldsMapping[roleType]);
            worksheet.Cell(row, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            worksheet.Cell(row, 2).Style.Alignment.WrapText = true;
            worksheet.Cell(row, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            row++;
        }

        // 注意事项
        row += 1;
        worksheet.Cell(row, 1).Value = "注意事项";
        worksheet.Range(row, 1, row, 2).Merge();
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        worksheet.Cell(row, 1).Style.Font.FontSize = 12;
        row++;

        var notes = new[]
        {
            "1. 每个角色类型对应一个独立 Sheet，请在该 Sheet 内填写数据。",
            "2. \"所属院校\"为 Sheet 内统一名称，导入时按名称解析；解析失败的行将被标记为失败。",
            "3. 学生的\"专业\"按名称解析为系统内 MajorId；教师\"所教专业\"以字符串保存。",
            "4. 必填字段为空、账号重复、邮箱格式不合法时，该行会被跳过并在结果中列出失败原因。",
            "5. 初始密码建议首次登录后由用户自行修改，避免长期使用默认密码。",
            "6. 上传前请删除示例行；多 Sheet 同时上传会被一并处理。",
        };
        foreach (var note in notes)
        {
            worksheet.Cell(row, 1).Value = note;
            worksheet.Range(row, 1, row, 2).Merge();
            worksheet.Cell(row, 1).Style.Alignment.WrapText = true;
            row++;
        }

        worksheet.Column(1).Width = 20;
        worksheet.Column(2).Width = 80;
        worksheet.SheetView.FreezeRows(3);
    }
}
