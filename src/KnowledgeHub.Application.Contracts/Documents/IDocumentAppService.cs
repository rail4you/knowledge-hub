using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using KnowledgeHub.Documents;

namespace KnowledgeHub.Documents;

public interface IDocumentAppService :
    ICrudAppService< 
        DocumentDto, 
        Guid, 
        PagedAndSortedResultRequestDto, 
        CreateUpdateDocumentDto> 
{
    Task<ListResultDto<UserLookupDto>> GetUserLookupAsync();
}
