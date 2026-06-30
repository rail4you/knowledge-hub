using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Practicums.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Practicums;

public interface IPracticumChatAppService : IApplicationService
{
    Task<PracticumChatMessageDto> SendAsync(SendPracticumChatMessageDto input);
    Task<List<PracticumChatMessageDto>> GetMessagesAsync(GetPracticumChatMessagesDto input);
}
