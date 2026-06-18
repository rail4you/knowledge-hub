using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using KnowledgeHub.Majors;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Identity;
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

    public UserImportAppService(
        IIdentityUserRepository identityUserRepository,
        IdentityUserManager identityUserManager,
        IIdentityRoleRepository identityRoleRepository,
        IMajorRepository majorRepository)
    {
        _identityUserRepository = identityUserRepository;
        _identityUserManager = identityUserManager;
        _identityRoleRepository = identityRoleRepository;
        _majorRepository = majorRepository;
    }

    [UnitOfWork]
    public async Task<UserImportResultDto> ImportAsync(byte[] excelFile)
    {
        var result = new UserImportResultDto();

        using var stream = new System.IO.MemoryStream(excelFile);
        using var workbook = new XLWorkbook(stream);

        foreach (var sheetName in SheetRoleMapping.Keys)
        {
            var worksheet = workbook.Worksheet(sheetName);
            if (worksheet == null) continue;

            var roleType = SheetRoleMapping[sheetName];
            var rows = worksheet.RangeUsed()?.RowsUsed().Skip(1); // Skip header
            if (rows == null) continue;

            var rowNumber = 2; // Start from row 2 (row 1 is header)
            foreach (var row in rows)
            {
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
            result.TotalCount += rowNumber - 2;
        }

        return result;
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
        var user = new IdentityUser(
            GuidGenerator.Create(),
            dto.UserName,
            dto.Email ?? $"{dto.UserName}@default.com"
        );

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
        if (dto.RoleType == UserRoleType.Student && !string.IsNullOrWhiteSpace(dto.Major))
        {
            var major = await _majorRepository.FindByNameAsync(dto.Major);
            if (major == null)
            {
                throw new UserFriendlyException($"专业【{dto.Major}】不存在，请先在专业管理中创建");
            }
            user.ExtraProperties[MajorIdExtraProperty] = major.Id;
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
        var role = await _identityRoleRepository.FindByNormalizedNameAsync(roleName.ToUpperInvariant());
        if (role != null)
        {
            user.AddRole(role.Id);
        }

        await _identityUserRepository.UpdateAsync(user);
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
}
