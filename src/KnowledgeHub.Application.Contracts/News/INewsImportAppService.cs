using System.Threading.Tasks;
using KnowledgeHub.News.Dtos;
using Microsoft.AspNetCore.Http;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace KnowledgeHub.News;

public interface INewsImportAppService : IApplicationService
{
    Task<NewsImportResultDto> ImportAsync(IFormFile file);

    /// <summary>
    /// 生成资讯导入 Excel 模板（P1-13 修复）
    /// </summary>
    Task<IRemoteStreamContent> DownloadTemplateAsync();
}
