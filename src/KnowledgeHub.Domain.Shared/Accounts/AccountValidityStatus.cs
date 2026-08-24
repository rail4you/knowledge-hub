namespace KnowledgeHub.Accounts;

/// <summary>
/// 账号有效期状态
/// </summary>
public enum AccountValidityStatus
{
    /// <summary>有效期内（未到期）</summary>
    Active = 0,

    /// <summary>已到期（编辑/管理权限已被熔断收回）</summary>
    Expired = 1
}
