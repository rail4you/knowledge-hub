using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Application.AI.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Application.AI;

public interface IChatAppService : IApplicationService
{
    Task<ChatThreadDto> CreateThreadAsync();
    Task<ChatThreadDto> GetThreadAsync(string threadId);
    Task<List<ChatThreadDto>> GetMyThreadsAsync();
    Task<List<ResourceForChatDto>> GetResourcesWithPageIndexAsync();
}
