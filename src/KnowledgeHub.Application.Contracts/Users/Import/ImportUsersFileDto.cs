using System;

namespace KnowledgeHub.Users;

/// <summary>
/// 用户批量导入文件输入：xlsx 文件内容的 Base64 编码。
/// 说明：ASP.NET Core 默认 JSON 序列化（System.Text.Json）无法把数字数组反序列化为 byte[]，
/// 只能接受 Base64 字符串，因此导入接口统一用本 DTO 传文件（与就业模块 ImportJobsInput 一致）。
/// </summary>
public class ImportUsersFileDto
{
    /// <summary>xlsx 文件内容的 Base64 编码</summary>
    public string FileBase64 { get; set; } = string.Empty;

    /// <summary>原始文件名（仅用于提示，可选）</summary>
    public string? FileName { get; set; }

    /// <summary>
    /// 仅供 host 管理员使用：当导入用户归属某个租户时传入，租户管理员忽略此字段（自动用自己所在租户）。
    /// 留空时表示导入 host 全局用户（仅当 角色类型=联盟管理员/企业用户 时合法）。
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// 是否覆盖已存在的同名用户。
    /// true：遇到同名用户时更新其姓名/手机号/邮箱/扩展属性并保留角色；
    /// false：遇到同名用户时该行标记为 跳过，导入不会被中断。
    /// </summary>
    public bool OverwriteExisting { get; set; } = false;
}
