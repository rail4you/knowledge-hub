using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.DoubleHigh.Dtos;
using KnowledgeHub.DoubleHigh.Enums;
using Microsoft.AspNetCore.Http;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace KnowledgeHub.DoubleHigh;

public interface IDoubleHighAppService : IApplicationService
{
    Task<DoubleHighProjectDto> GetAsync(Guid id);
    Task<DoubleHighProjectDetailDto> GetDetailAsync(Guid id);
    Task<PagedResultDto<DoubleHighProjectDto>> GetListAsync(PagedDoubleHighProjectRequestDto input);
    Task<DoubleHighProjectDto> CreateAsync(CreateUpdateDoubleHighProjectDto input);
    Task<DoubleHighProjectDto> UpdateAsync(Guid id, CreateUpdateDoubleHighProjectDto input);
    Task DeleteAsync(Guid id);

    /// <summary>获取评估模板目录（新建项目时选择模板，预置指标与权重）。</summary>
    Task<List<DoubleHighTemplateDto>> GetTemplatesAsync();

    /// <summary>获取评估对象候选项（教学资源库 / 专业 / 课程），用于选择评估对象与取值范围。</summary>
    Task<List<DoubleHighObjectOptionDto>> GetObjectOptionsAsync(DoubleHighObjectType objectType, string? filter);

    /// <summary>按模板创建评估项目（指标、权重、评分规则一并预置）。</summary>
    Task<DoubleHighProjectDetailDto> CreateFromTemplateAsync(CreateDoubleHighProjectFromTemplateDto input);

    /// <summary>开始填报（配置定稿，指标配置锁定）。</summary>
    Task<DoubleHighProjectDto> StartFillingAsync(Guid id);

    /// <summary>提交填报（全部指标完成后提交，提交后内容锁定）。</summary>
    Task<DoubleHighProjectDto> SubmitAsync(Guid id);

    Task<DoubleHighDashboardDto> CollectProjectAsync(Guid projectId);
    Task<decimal> GetDataSourcePreviewAsync(DoubleHighDataSourceType dataSourceType, DoubleHighObjectType? scopeType = null, Guid? scopeId = null);
    Task<DoubleHighIndicatorDto> AddIndicatorAsync(Guid projectId, CreateUpdateDoubleHighIndicatorDto input);
    Task<DoubleHighIndicatorDto> UpdateIndicatorAsync(Guid id, CreateUpdateDoubleHighIndicatorDto input);
    Task DeleteIndicatorAsync(Guid id);
    Task MoveIndicatorAsync(Guid id, bool up);

    /// <summary>保存教师填报内容（数值 / 说明 / 自评备注）。</summary>
    Task<DoubleHighIndicatorDto> SaveReportingAsync(SaveDoubleHighReportingDto input);

    Task<DoubleHighIndicatorValueSnapshotDto> SaveManualValueAsync(SaveDoubleHighIndicatorValueDto input);
    Task<DoubleHighEvidenceDto> AddEvidenceAsync(CreateDoubleHighEvidenceDto input);
    Task<DoubleHighEvidenceDto> UpdateEvidenceAsync(Guid id, CreateDoubleHighEvidenceDto input);
    Task DeleteEvidenceAsync(Guid id);

    /// <summary>上传佐证文件（教师在填报页直接上传，文件落到对象存储 / 本地磁盘）。</summary>
    Task<DoubleHighEvidenceDto> AddEvidenceFileAsync(
        Guid projectId,
        Guid indicatorId,
        string? evidenceName,
        IFormFile file);

    /// <summary>上传项目依据文件（政策文件等，可多个）。</summary>
    Task<DoubleHighProjectDocumentDto> AddProjectDocumentAsync(Guid projectId, IFormFile file, string? title);

    /// <summary>删除项目依据文件（同时清掉引用它的指标依据）。</summary>
    Task DeleteProjectDocumentAsync(Guid documentId);

    /// <summary>下载项目依据文件原件。</summary>
    Task<IRemoteStreamContent> DownloadProjectDocumentAsync(Guid documentId);

    Task<PagedResultDto<DoubleHighReportDto>> GetReportListAsync(GetDoubleHighReportsInput input);
    Task<IRemoteStreamContent> ExportReportAsync(Guid projectId);
    Task<IRemoteStreamContent> DownloadReportAsync(Guid reportId);

    /// <summary>导出单个评估项目的资料包（zip：评估数据 + 佐证文件）。</summary>
    Task<IRemoteStreamContent> ExportPackageAsync(Guid projectId);

    /// <summary>导出全部评估项目的资料包（zip：按项目分目录）。</summary>
    Task<IRemoteStreamContent> ExportAllPackageAsync();
}
