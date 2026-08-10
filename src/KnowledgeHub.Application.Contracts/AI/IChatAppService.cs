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

    /// <summary>
    /// 删除单个聊天线程及其所有消息。
    /// </summary>
    Task DeleteThreadAsync(Guid threadId);

    /// <summary>
    /// 清空当前用户所有聊天线程。
    /// </summary>
    Task ClearAllThreadsAsync();

    /// <summary>
    /// 保存聊天消息到线程（在流式完成后调用）。
    /// </summary>
    Task SaveMessagesAsync(Guid threadId, string? title, Guid? resourceId, List<ChatMessageDto> messages);
}
