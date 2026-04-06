using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Application.AI.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Application.AI;

public interface IChatAppService : IApplicationService
{
    Task<ChatThreadDto> CreateThreadAsync();
    IAsyncEnumerable<ChatMessageChunkDto> ChatStreamingAsync(ChatInputDto input);
    Task<ChatThreadDto> GetThreadAsync(string threadId);
    Task<List<ChatThreadDto>> GetMyThreadsAsync();
    Task<List<ResourceForChatDto>> GetResourcesWithPageIndexAsync();
}
