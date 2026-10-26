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
    /// <summary>
    /// 可选资源列表（仅已建索引的资源，供 AI 文档问答 / 教案 / 案例分析选择）。
    /// </summary>
    /// <param name="libraryId">教学资源库筛选；为空表示全部（含未分类）。</param>
    Task<List<ResourceForChatDto>> GetResourcesWithPageIndexAsync(Guid? libraryId = null);

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
