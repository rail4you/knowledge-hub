using System;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Application.Contracts.Search;

public interface IVideoAnalysisAppService : IApplicationService
{
    Task<VideoAnalysisResultDto> AnalyzeVideoTimelineAsync(VideoAnalysisRequestDto input);
    Task<VideoAnalysisResultDto> AnalyzeLocalVideoAsync(string filePath);
    Task SaveVideoTimelineToMeiliSearchAsync(Guid videoId, string videoName, string videoUrl, VideoAnalysisResultDto analysisResult);
}
