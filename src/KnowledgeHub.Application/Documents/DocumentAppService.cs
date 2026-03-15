using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using KnowledgeHub.Permissions;
using KnowledgeHub.Users;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.Documents;

[Authorize(KnowledgeHubPermissions.Documents.Default)]
public class DocumentAppService :
    CrudAppService<
        Document, 
        DocumentDto, 
        Guid, 
        PagedAndSortedResultRequestDto, 
        CreateUpdateDocumentDto>, 
    IDocumentAppService
{
    private readonly IUserRepository _userRepository;

    public DocumentAppService(
        IRepository<Document, Guid> repository,
        IUserRepository userRepository)
        : base(repository)
    {
        _userRepository = userRepository;
        GetPolicyName = KnowledgeHubPermissions.Documents.Default;
        GetListPolicyName = KnowledgeHubPermissions.Documents.Default;
        CreatePolicyName = KnowledgeHubPermissions.Documents.Create;
        UpdatePolicyName = KnowledgeHubPermissions.Documents.Edit;
        DeletePolicyName = KnowledgeHubPermissions.Documents.Delete;
    }

    public override async Task<DocumentDto> GetAsync(Guid id)
    {
        var queryable = await Repository.GetQueryableAsync();

        var query = from document in queryable
            join user in await _userRepository.GetQueryableAsync() on document.UserId equals user.Id
            where document.Id == id
            select new { document, user };

        var queryResult = await AsyncExecuter.FirstOrDefaultAsync(query);
        if (queryResult == null)
        {
            throw new EntityNotFoundException(typeof(Document), id);
        }

        var documentDto = ObjectMapper.Map<Document, DocumentDto>(queryResult.document);
        documentDto.UserName = queryResult.user.Name;
        return documentDto;
    }

    public override async Task<PagedResultDto<DocumentDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var queryable = await Repository.GetQueryableAsync();

        var query = from document in queryable
            join user in await _userRepository.GetQueryableAsync() on document.UserId equals user.Id
            select new {document, user};

        query = query
            .OrderBy(NormalizeSorting(input.Sorting))
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount);

        var queryResult = await AsyncExecuter.ToListAsync(query);

        var documentDtos = queryResult.Select(x =>
        {
            var documentDto = ObjectMapper.Map<Document, DocumentDto>(x.document);
            documentDto.UserName = x.user.Name;
            return documentDto;
        }).ToList();

        var totalCount = await Repository.GetCountAsync();

        return new PagedResultDto<DocumentDto>(
            totalCount,
            documentDtos
        );
    }

    public async Task<ListResultDto<UserLookupDto>> GetUserLookupAsync()
    {
        var users = await _userRepository.GetListAsync();

        return new ListResultDto<UserLookupDto>(
            ObjectMapper.Map<List<AppUser>, List<UserLookupDto>>(users)
        );
    }

    private static string NormalizeSorting(string sorting)
    {
        if (sorting.IsNullOrEmpty())
        {
            return $"document.{nameof(Document.Name)}";
        }

        if (sorting.Contains("userName", StringComparison.OrdinalIgnoreCase))
        {
            return sorting.Replace(
                "userName",
                "user.Name",
                StringComparison.OrdinalIgnoreCase
            );
        }

        return $"document.{sorting}";
    }
}
