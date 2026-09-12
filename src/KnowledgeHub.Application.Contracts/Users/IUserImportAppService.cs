using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Users;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace KnowledgeHub.Users;

public interface IUserImportAppService : IApplicationService
{
    /// <summary>
    /// 解析 Excel 并返回预览：每行将做什么（新建/覆盖/跳过/失败），
    /// 但不会真正写入数据库。可在用户确认后再调用 ImportAsync 落地。
    /// 自动路由：POST /api/app/user-import/preview
    /// </summary>
    Task<UserImportResultDto> PreviewAsync(ImportUsersFileDto input);

    /// <summary>
    /// 实际导入：根据 input.OverwriteExisting 决定遇到同名用户时是覆盖还是跳过；
    /// 返回值与 PreviewImportAsync 同结构（Status 表示实际做了什么）。
    /// </summary>
    Task<UserImportResultDto> ImportAsync(ImportUsersFileDto input);

    /// <summary>
    /// P1-2：返回系统预置的「角色 → 中文名 → 已授予权限数」清单，便于管理员核对 SchoolAdmin vs LeagueAdmin 等角色的实际差异
    /// </summary>
    Task<List<RolePermissionSummaryDto>> GetRolePermissionSummaryAsync();

    /// <summary>
    /// 生成用户批量导入模板（xlsx）：单 Sheet，第 1 行标题、第 2 行说明、第 3 行表头、之后为示例行。
    /// 必填列高亮；第 1 列 "角色类型" 用于区分不同角色用户的必填字段。
    /// </summary>
    Task<IRemoteStreamContent> GetImportTemplateAsync();
}
