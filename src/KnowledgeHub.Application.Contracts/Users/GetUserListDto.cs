using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Users;

public class GetUserListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
}
