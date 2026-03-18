using System.Collections.Generic;

namespace KnowledgeHub.Users;

public class UserImportResultDto
{
    public int TotalCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public List<UserImportFailItemDto> FailItems { get; set; } = new();
}

public class UserImportFailItemDto
{
    public int RowNumber { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
