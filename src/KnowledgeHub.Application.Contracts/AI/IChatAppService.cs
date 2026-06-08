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
    /// P1-13：获取当前用户"作为简历"使用的、已审核通过的资源。
    /// 用于 AI 职业规划下拉（仅显示当前用户上传的简历资源）。
    /// </summary>
    Task<List<ResourceForChatDto>> GetResumesForUserAsync();
}
