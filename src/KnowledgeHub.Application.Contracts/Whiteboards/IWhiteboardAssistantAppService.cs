using System.Threading.Tasks;
using KnowledgeHub.Whiteboards.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Whiteboards;

/// <summary>
/// 白板 AI 助手（课程助教）。
///
/// <para>它不是一个通用聊天窗口，而是**贴着这块白板**的助教：知道白板关联的课程、
/// 画布上已经写了什么、老师们在右侧协作频道里聊过什么，据此产出能直接贴到白板上的文字，
/// 或者生成一张教学配图。</para>
/// </summary>
public interface IWhiteboardAssistantAppService : IApplicationService
{
    /// <summary>向助手提问 / 要求生成内容。</summary>
    Task<WhiteboardAssistantReplyDto> AskAsync(WhiteboardAssistantAskInputDto input);
}
