using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ClosedXML.Excel;
using KnowledgeHub.Courses;
using KnowledgeHub.Courses.Enums;
using KnowledgeHub.DoubleHigh.Dtos;
using KnowledgeHub.DoubleHigh.Enums;
using KnowledgeHub.MicroMajors;
using KnowledgeHub.MicroMajors.Enums;
using KnowledgeHub.News;
using KnowledgeHub.News.Enums;
using KnowledgeHub.Permissions;
using KnowledgeHub.Practicums;
using KnowledgeHub.Practicums.Enums;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Content;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.DoubleHigh;

public class DoubleHighAppService : KnowledgeHubAppService, IDoubleHighAppService
{
    private readonly IRepository<DoubleHighProject, Guid> _projectRepository;
    private readonly IRepository<DoubleHighIndicator, Guid> _indicatorRepository;
    private readonly IRepository<DoubleHighIndicatorValue, Guid> _valueRepository;
    private readonly IRepository<DoubleHighEvidence, Guid> _evidenceRepository;
    private readonly IRepository<DoubleHighReport, Guid> _reportRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<MicroMajor, Guid> _microMajorRepository;
    private readonly IRepository<MicroMajorEnrollment, Guid> _microMajorEnrollmentRepository;
    private readonly IRepository<PracticumProject, Guid> _practicumProjectRepository;
    private readonly IRepository<PracticumEnrollment, Guid> _practicumEnrollmentRepository;
    private readonly IRepository<NewsArticle, Guid> _newsArticleRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;

    public DoubleHighAppService(
        IRepository<DoubleHighProject, Guid> projectRepository,
        IRepository<DoubleHighIndicator, Guid> indicatorRepository,
        IRepository<DoubleHighIndicatorValue, Guid> valueRepository,
        IRepository<DoubleHighEvidence, Guid> evidenceRepository,
        IRepository<DoubleHighReport, Guid> reportRepository,
        IRepository<Resource, Guid> resourceRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<MicroMajor, Guid> microMajorRepository,
        IRepository<MicroMajorEnrollment, Guid> microMajorEnrollmentRepository,
        IRepository<PracticumProject, Guid> practicumProjectRepository,
        IRepository<PracticumEnrollment, Guid> practicumEnrollmentRepository,
        IRepository<NewsArticle, Guid> newsArticleRepository,
        IRepository<IdentityUser, Guid> userRepository)
    {
        _projectRepository = projectRepository;
        _indicatorRepository = indicatorRepository;
        _valueRepository = valueRepository;
        _evidenceRepository = evidenceRepository;
        _reportRepository = reportRepository;
        _resourceRepository = resourceRepository;
        _courseRepository = courseRepository;
        _microMajorRepository = microMajorRepository;
        _microMajorEnrollmentRepository = microMajorEnrollmentRepository;
        _practicumProjectRepository = practicumProjectRepository;
        _practicumEnrollmentRepository = practicumEnrollmentRepository;
        _newsArticleRepository = newsArticleRepository;
        _userRepository = userRepository;
    }

    [Authorize(KnowledgeHubPermissions.DoubleHigh.Default)]
    public async Task<DoubleHighProjectDto> GetAsync(Guid id)
    {
        var entity = await GetProjectEntityAsync(id);
        return await MapProjectDtoAsync(entity);
    }

    [Authorize(KnowledgeHubPermissions.DoubleHigh.Default)]
    public async Task<DoubleHighProjectDetailDto> GetDetailAsync(Guid id)
    {
        var entity = await GetProjectEntityAsync(id);
        return await MapProjectDetailDtoAsync(entity);
    }

    [Authorize(KnowledgeHubPermissions.DoubleHigh.Default)]
    public async Task<PagedResultDto<DoubleHighProjectDto>> GetListAsync(PagedDoubleHighProjectRequestDto input)
    {
        var canViewAll = await CanViewAllAsync();
        List<DoubleHighProject> items;
        long totalCount;

        if (canViewAll)
        {
            using (DataFilter.Disable<IMultiTenant>())
            {
                var query = await _projectRepository.GetQueryableAsync();
                query = ApplyProjectFilters(query, input);
                totalCount = await query.LongCountAsync();
                items = await query
                    .OrderByDescending(x => x.CreationTime)
                    .Skip(input.SkipCount)
                    .Take(input.MaxResultCount)
                    .ToListAsync();
            }
        }
        else
        {
            var query = await _projectRepository.GetQueryableAsync();
            query = ApplyProjectFilters(query, input);
            totalCount = await query.LongCountAsync();
            items = await query
                .OrderByDescending(x => x.CreationTime)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
                .ToListAsync();
        }

        return new PagedResultDto<DoubleHighProjectDto>(totalCount, await MapProjectDtosAsync(items));
    }

    [Authorize(KnowledgeHubPermissions.DoubleHigh.ManageProject)]
    public async Task<DoubleHighProjectDto> CreateAsync(CreateUpdateDoubleHighProjectDto input)
    {
        ValidateProjectInput(input);

        var entity = new DoubleHighProject(GuidGenerator.Create(), input.Title.Trim(), input.BatchCode.Trim())
        {
            TenantId = CurrentTenant.Id,
            Description = input.Description?.Trim(),
            Status = input.Status,
            StartTime = input.StartTime,
            EndTime = input.EndTime
        };

        await _projectRepository.InsertAsync(entity, autoSave: true);
        await ReplaceIndicatorsAsync(entity.Id, input.Indicators);
        return await MapProjectDtoAsync(entity);
    }

    [Authorize(KnowledgeHubPermissions.DoubleHigh.ManageProject)]
    public async Task<DoubleHighProjectDto> UpdateAsync(Guid id, CreateUpdateDoubleHighProjectDto input)
    {
        ValidateProjectInput(input);

        var entity = await GetProjectEntityAsync(id);
        entity.Title = input.Title.Trim();
        entity.BatchCode = input.BatchCode.Trim();
        entity.Description = input.Description?.Trim();
        entity.Status = input.Status;
        entity.StartTime = input.StartTime;
        entity.EndTime = input.EndTime;

        await _projectRepository.UpdateAsync(entity, autoSave: true);
        await ReplaceIndicatorsAsync(id, input.Indicators);
        return await MapProjectDtoAsync(entity);
    }

    [Authorize(KnowledgeHubPermissions.DoubleHigh.ManageProject)]
    public async Task DeleteAsync(Guid id)
    {
        var indicators = await _indicatorRepository.GetListAsync(x => x.ProjectId == id);
        foreach (var indicator in indicators)
        {
            var values = await _valueRepository.GetListAsync(x => x.IndicatorId == indicator.Id);
            foreach (var value in values)
            {
                await _valueRepository.DeleteAsync(value);
            }
            await _indicatorRepository.DeleteAsync(indicator);
        }

        var evidences = await _evidenceRepository.GetListAsync(x => x.ProjectId == id);
        foreach (var evidence in evidences)
        {
            await _evidenceRepository.DeleteAsync(evidence);
        }

        var reports = await _reportRepository.GetListAsync(x => x.ProjectId == id);
        foreach (var report in reports)
        {
            await _reportRepository.DeleteAsync(report);
        }

        await _projectRepository.DeleteAsync(id);
    }

    [Authorize(KnowledgeHubPermissions.DoubleHigh.CollectData)]
    public async Task<DoubleHighDashboardDto> CollectProjectAsync(Guid projectId)
    {
        var project = await GetProjectEntityAsync(projectId);
        var indicators = await _indicatorRepository.GetListAsync(x => x.ProjectId == projectId);

        foreach (var indicator in indicators.Where(x => x.DataSourceType != DoubleHighDataSourceType.Manual))
        {
            var value = await CalculateIndicatorValueAsync(project.TenantId, indicator.DataSourceType);
            var entity = new DoubleHighIndicatorValue(GuidGenerator.Create(), projectId, indicator.Id, value)
            {
                TenantId = project.TenantId,
                SourceType = DoubleHighValueSourceType.Automatic,
                Note = $"AutoCollected:{indicator.DataSourceType}"
            };
            await _valueRepository.InsertAsync(entity);
        }

        project.LastCollectedAt = DateTime.UtcNow;
        await _projectRepository.UpdateAsync(project, autoSave: true);
        return await BuildDashboardAsync(project);
    }

    [Authorize(KnowledgeHubPermissions.DoubleHigh.CollectData)]
    public async Task<DoubleHighIndicatorValueSnapshotDto> SaveManualValueAsync(SaveDoubleHighIndicatorValueDto input)
    {
        var indicator = await _indicatorRepository.GetAsync(input.IndicatorId);
        if (indicator.DataSourceType != DoubleHighDataSourceType.Manual)
        {
            throw new UserFriendlyException("当前指标为自动采集项，不能手工填报。");
        }

        var entity = new DoubleHighIndicatorValue(GuidGenerator.Create(), indicator.ProjectId, indicator.Id, input.Value)
        {
            TenantId = CurrentTenant.Id,
            SourceType = DoubleHighValueSourceType.Manual,
            Note = input.Note?.Trim()
        };

        await _valueRepository.InsertAsync(entity, autoSave: true);
        return MapValueSnapshot(entity);
    }

    [Authorize(KnowledgeHubPermissions.DoubleHigh.ManageIndicator)]
    public async Task<DoubleHighEvidenceDto> AddEvidenceAsync(CreateDoubleHighEvidenceDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new UserFriendlyException("佐证材料标题不能为空。");
        }

        var indicator = await _indicatorRepository.GetAsync(input.IndicatorId);
        if (indicator.ProjectId != input.ProjectId)
        {
            throw new UserFriendlyException("佐证材料所属项目和指标不匹配。");
        }

        if (input.EvidenceType == DoubleHighEvidenceType.ResourceLink && !input.ResourceId.HasValue)
        {
            throw new UserFriendlyException("资源型佐证材料必须绑定资源。");
        }

        if (input.ResourceId.HasValue)
        {
            await _resourceRepository.GetAsync(input.ResourceId.Value);
        }

        var entity = new DoubleHighEvidence(GuidGenerator.Create(), input.ProjectId, input.IndicatorId, input.Title.Trim())
        {
            TenantId = CurrentTenant.Id,
            Description = input.Description?.Trim(),
            EvidenceType = input.EvidenceType,
            ResourceId = input.ResourceId,
            AttachmentUrl = input.AttachmentUrl?.Trim(),
            ExternalLink = input.ExternalLink?.Trim(),
            SortOrder = input.SortOrder
        };

        await _evidenceRepository.InsertAsync(entity, autoSave: true);
        return await MapEvidenceDtoAsync(entity);
    }

    [Authorize(KnowledgeHubPermissions.DoubleHigh.ManageIndicator)]
    public async Task DeleteEvidenceAsync(Guid id)
    {
        await _evidenceRepository.DeleteAsync(id);
    }

    [Authorize(KnowledgeHubPermissions.DoubleHigh.Default)]
    public async Task<PagedResultDto<DoubleHighReportDto>> GetReportListAsync(GetDoubleHighReportsInput input)
    {
        var canViewAll = await CanViewAllAsync();
        List<DoubleHighReport> items;
        long totalCount;

        if (canViewAll)
        {
            using (DataFilter.Disable<IMultiTenant>())
            {
                var query = await _reportRepository.GetQueryableAsync();
                query = query.WhereIf(input.ProjectId.HasValue, x => x.ProjectId == input.ProjectId.Value);
                totalCount = await query.LongCountAsync();
                items = await query
                    .OrderByDescending(x => x.GeneratedAt)
                    .Skip(input.SkipCount)
                    .Take(input.MaxResultCount)
                    .ToListAsync();
            }
        }
        else
        {
            var query = await _reportRepository.GetQueryableAsync();
            query = query.WhereIf(input.ProjectId.HasValue, x => x.ProjectId == input.ProjectId.Value);
            totalCount = await query.LongCountAsync();
            items = await query
                .OrderByDescending(x => x.GeneratedAt)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
                .ToListAsync();
        }

        return new PagedResultDto<DoubleHighReportDto>(totalCount, await MapReportDtosAsync(items));
    }

    [Authorize(KnowledgeHubPermissions.DoubleHigh.ExportReport)]
    public async Task<IRemoteStreamContent> ExportReportAsync(Guid projectId)
    {
        var detail = await GetDetailAsync(projectId);
        using var workbook = new XLWorkbook();

        var overview = workbook.Worksheets.Add("项目概览");
        overview.Cell(1, 1).Value = "项目名称";
        overview.Cell(1, 2).Value = detail.Title;
        overview.Cell(2, 1).Value = "批次编码";
        overview.Cell(2, 2).Value = detail.BatchCode;
        overview.Cell(3, 1).Value = "状态";
        overview.Cell(3, 2).Value = detail.Status.ToString();
        overview.Cell(4, 1).Value = "指标总数";
        overview.Cell(4, 2).Value = detail.Dashboard.TotalIndicators;
        overview.Cell(5, 1).Value = "已采集指标数";
        overview.Cell(5, 2).Value = detail.Dashboard.CollectedIndicators;
        overview.Cell(6, 1).Value = "完成率";
        overview.Cell(6, 2).Value = detail.Dashboard.CompletionRate;
        overview.Cell(7, 1).Value = "佐证材料数";
        overview.Cell(7, 2).Value = detail.Dashboard.EvidenceCount;
        overview.Columns().AdjustToContents();

        var indicatorSheet = workbook.Worksheets.Add("指标数据");
        indicatorSheet.Cell(1, 1).Value = "分类";
        indicatorSheet.Cell(1, 2).Value = "指标编码";
        indicatorSheet.Cell(1, 3).Value = "指标名称";
        indicatorSheet.Cell(1, 4).Value = "数据来源";
        indicatorSheet.Cell(1, 5).Value = "目标值";
        indicatorSheet.Cell(1, 6).Value = "最新值";
        indicatorSheet.Cell(1, 7).Value = "单位";
        indicatorSheet.Cell(1, 8).Value = "采集时间";
        indicatorSheet.Cell(1, 9).Value = "备注";
        ApplyHeaderStyle(indicatorSheet, 1, 9);

        for (var i = 0; i < detail.Indicators.Count; i++)
        {
            var row = i + 2;
            var item = detail.Indicators[i];
            indicatorSheet.Cell(row, 1).Value = item.CategoryName;
            indicatorSheet.Cell(row, 2).Value = item.IndicatorCode;
            indicatorSheet.Cell(row, 3).Value = item.Name;
            indicatorSheet.Cell(row, 4).Value = item.DataSourceType.ToString();
            indicatorSheet.Cell(row, 5).Value = item.TargetValue;
            indicatorSheet.Cell(row, 6).Value = item.LatestValue?.Value;
            indicatorSheet.Cell(row, 7).Value = item.Unit;
            indicatorSheet.Cell(row, 8).Value = item.LatestValue?.CollectedAt.ToString("yyyy-MM-dd HH:mm") ?? string.Empty;
            indicatorSheet.Cell(row, 9).Value = item.LatestValue?.Note;
        }
        indicatorSheet.Columns().AdjustToContents();

        var evidenceSheet = workbook.Worksheets.Add("佐证材料");
        evidenceSheet.Cell(1, 1).Value = "指标";
        evidenceSheet.Cell(1, 2).Value = "标题";
        evidenceSheet.Cell(1, 3).Value = "类型";
        evidenceSheet.Cell(1, 4).Value = "资源名称";
        evidenceSheet.Cell(1, 5).Value = "链接";
        evidenceSheet.Cell(1, 6).Value = "说明";
        ApplyHeaderStyle(evidenceSheet, 1, 6);

        for (var i = 0; i < detail.Evidences.Count; i++)
        {
            var row = i + 2;
            var item = detail.Evidences[i];
            evidenceSheet.Cell(row, 1).Value = item.IndicatorName;
            evidenceSheet.Cell(row, 2).Value = item.Title;
            evidenceSheet.Cell(row, 3).Value = item.EvidenceType.ToString();
            evidenceSheet.Cell(row, 4).Value = item.ResourceName;
            evidenceSheet.Cell(row, 5).Value = item.AttachmentUrl ?? item.ExternalLink ?? string.Empty;
            evidenceSheet.Cell(row, 6).Value = item.Description;
        }
        evidenceSheet.Columns().AdjustToContents();

        var reportName = $"双高评估报表_{detail.BatchCode}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
        var report = new DoubleHighReport(GuidGenerator.Create(), projectId, reportName)
        {
            TenantId = CurrentTenant.Id,
            GeneratedById = CurrentUser.Id,
            SummaryJson = JsonSerializer.Serialize(detail.Dashboard)
        };
        await _reportRepository.InsertAsync(report, autoSave: true);

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(
            stream,
            reportName,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    private IQueryable<DoubleHighProject> ApplyProjectFilters(
        IQueryable<DoubleHighProject> query,
        PagedDoubleHighProjectRequestDto input)
    {
        return query
            .WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x =>
                x.Title.Contains(input.Filter!) ||
                x.BatchCode.Contains(input.Filter!) ||
                (x.Description != null && x.Description.Contains(input.Filter!)))
            .WhereIf(input.Status.HasValue, x => x.Status == input.Status.Value);
    }

    private void ValidateProjectInput(CreateUpdateDoubleHighProjectDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new UserFriendlyException("评估项目名称不能为空。");
        }

        if (string.IsNullOrWhiteSpace(input.BatchCode))
        {
            throw new UserFriendlyException("申报批次编码不能为空。");
        }

        if (input.Indicators.Count == 0)
        {
            throw new UserFriendlyException("至少需要配置一个指标项。");
        }
    }

    private async Task ReplaceIndicatorsAsync(Guid projectId, List<CreateUpdateDoubleHighIndicatorDto> inputs)
    {
        var existing = await _indicatorRepository.GetListAsync(x => x.ProjectId == projectId);
        foreach (var indicator in existing)
        {
            var values = await _valueRepository.GetListAsync(x => x.IndicatorId == indicator.Id);
            foreach (var value in values)
            {
                await _valueRepository.DeleteAsync(value);
            }
            await _indicatorRepository.DeleteAsync(indicator);
        }

        foreach (var input in inputs.OrderBy(x => x.SortOrder))
        {
            // 关键修复：DTO 上 CategoryName / IndicatorCode / Name 标注了非空且默认 string.Empty，
            // 但 ABP 反序列化 JSON 时如果前端送来 null，仍会得到 null；此时原实现直接 .Trim() 会
            // 抛 NullReferenceException → 500 → 前端只看到笼统"保存失败"。
            // 这里统一用空串兜底再 Trim，保证构造 DoubleHighIndicator 时不会 NRE。
            // 注：entity 构造函数的 code/name 参数理论上应该非空，但保持传入已 Trim 字符串以便兼容
            // 早期可能由其他调用方构造的 DTO。
            var code = (input.IndicatorCode ?? string.Empty).Trim();
            var name = (input.Name ?? string.Empty).Trim();
            var categoryName = (input.CategoryName ?? string.Empty).Trim();

            if (code.Length == 0 || name.Length == 0 || categoryName.Length == 0)
            {
                throw new UserFriendlyException(
                    $"指标 {input.SortOrder} 缺少必填字段：分类、编码、名称均不能为空。");
            }

            var entity = new DoubleHighIndicator(
                GuidGenerator.Create(),
                projectId,
                code,
                name)
            {
                TenantId = CurrentTenant.Id,
                ParentId = input.ParentId,
                CategoryName = categoryName,
                Description = input.Description?.Trim(),
                Unit = input.Unit?.Trim(),
                DataSourceType = input.DataSourceType,
                TargetValue = input.TargetValue,
                Weight = input.Weight,
                SortOrder = input.SortOrder
            };
            await _indicatorRepository.InsertAsync(entity);
        }
    }

    private async Task<DoubleHighProject> GetProjectEntityAsync(Guid id)
    {
        if (await CanViewAllAsync())
        {
            using (DataFilter.Disable<IMultiTenant>())
            {
                return await _projectRepository.GetAsync(id);
            }
        }

        return await _projectRepository.GetAsync(id);
    }

    private async Task<bool> CanViewAllAsync()
    {
        return CurrentTenant.Id == null &&
               await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.DoubleHigh.ViewAll);
    }

    private async Task<decimal> CalculateIndicatorValueAsync(Guid? tenantId, DoubleHighDataSourceType dataSourceType)
    {
        return dataSourceType switch
        {
            DoubleHighDataSourceType.ResourceCount => await CountResourcesAsync(tenantId),
            DoubleHighDataSourceType.CourseCount => await CountCoursesAsync(tenantId),
            DoubleHighDataSourceType.MicroMajorCount => await CountMicroMajorsAsync(tenantId),
            DoubleHighDataSourceType.PracticumProjectCount => await CountPracticumProjectsAsync(tenantId),
            DoubleHighDataSourceType.NewsArticleCount => await CountNewsArticlesAsync(tenantId),
            DoubleHighDataSourceType.MicroMajorEnrollmentCount => await CountMicroMajorEnrollmentsAsync(tenantId),
            DoubleHighDataSourceType.PracticumEnrollmentCount => await CountPracticumEnrollmentsAsync(tenantId),
            _ => 0
        };
    }

    private async Task<decimal> CountResourcesAsync(Guid? tenantId)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _resourceRepository.GetQueryableAsync();
            return await query
                .WhereIf(tenantId.HasValue, x => x.TenantId == tenantId.Value)
                .CountAsync(x => x.Status == ResourceStatus.SchoolApproved || x.Status == ResourceStatus.LeagueApproved);
        }
    }

    private async Task<decimal> CountCoursesAsync(Guid? tenantId)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _courseRepository.GetQueryableAsync();
            return await query
                .WhereIf(tenantId.HasValue, x => x.TenantId == tenantId.Value)
                .CountAsync(x => x.Status == CourseStatus.Published);
        }
    }

    private async Task<decimal> CountMicroMajorsAsync(Guid? tenantId)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _microMajorRepository.GetQueryableAsync();
            return await query
                .WhereIf(tenantId.HasValue, x => x.TenantId == tenantId.Value)
                .CountAsync(x => x.Status == MicroMajorStatus.Published);
        }
    }

    private async Task<decimal> CountPracticumProjectsAsync(Guid? tenantId)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _practicumProjectRepository.GetQueryableAsync();
            return await query
                .WhereIf(tenantId.HasValue, x => x.TenantId == tenantId.Value)
                .CountAsync(x => x.Status == PracticumProjectStatus.Published);
        }
    }

    private async Task<decimal> CountNewsArticlesAsync(Guid? tenantId)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _newsArticleRepository.GetQueryableAsync();
            return await query
                .WhereIf(tenantId.HasValue, x => x.TenantId == tenantId.Value)
                .CountAsync(x => x.Status == NewsArticleStatus.Published);
        }
    }

    private async Task<decimal> CountMicroMajorEnrollmentsAsync(Guid? tenantId)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _microMajorEnrollmentRepository.GetQueryableAsync();
            return await query
                .WhereIf(tenantId.HasValue, x => x.TenantId == tenantId.Value)
                .CountAsync(x => x.Status != MicroMajorEnrollmentStatus.Cancelled);
        }
    }

    private async Task<decimal> CountPracticumEnrollmentsAsync(Guid? tenantId)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _practicumEnrollmentRepository.GetQueryableAsync();
            return await query
                .WhereIf(tenantId.HasValue, x => x.TenantId == tenantId.Value)
                .CountAsync(x => x.Status != PracticumEnrollmentStatus.Cancelled);
        }
    }

    private async Task<DoubleHighDashboardDto> BuildDashboardAsync(DoubleHighProject project)
    {
        var indicators = await _indicatorRepository.GetListAsync(x => x.ProjectId == project.Id);
        var indicatorIds = indicators.Select(x => x.Id).ToList();
        var evidences = await _evidenceRepository.CountAsync(x => x.ProjectId == project.Id);
        var latestValues = await GetLatestValueMapAsync(indicatorIds);
        var collectedCount = indicatorIds.Count(id => latestValues.ContainsKey(id));

        return new DoubleHighDashboardDto
        {
            TotalIndicators = indicators.Count,
            ManualIndicators = indicators.Count(x => x.DataSourceType == DoubleHighDataSourceType.Manual),
            AutomaticIndicators = indicators.Count(x => x.DataSourceType != DoubleHighDataSourceType.Manual),
            CollectedIndicators = collectedCount,
            EvidenceCount = evidences,
            CompletionRate = indicators.Count == 0
                ? 0
                : Math.Round((decimal)collectedCount / indicators.Count * 100, 1),
            LastCollectedAt = project.LastCollectedAt
        };
    }

    private async Task<Dictionary<Guid, DoubleHighIndicatorValue>> GetLatestValueMapAsync(List<Guid> indicatorIds)
    {
        if (indicatorIds.Count == 0)
        {
            return new Dictionary<Guid, DoubleHighIndicatorValue>();
        }

        var items = await _valueRepository.GetListAsync(x => indicatorIds.Contains(x.IndicatorId));
        return items
            .GroupBy(x => x.IndicatorId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.CollectedAt).ThenByDescending(x => x.CreationTime).First());
    }

    private async Task<DoubleHighProjectDto> MapProjectDtoAsync(DoubleHighProject entity)
    {
        var dashboard = await BuildDashboardAsync(entity);
        return new DoubleHighProjectDto
        {
            Id = entity.Id,
            Title = entity.Title,
            BatchCode = entity.BatchCode,
            Description = entity.Description,
            Status = entity.Status,
            StartTime = entity.StartTime,
            EndTime = entity.EndTime,
            LastCollectedAt = entity.LastCollectedAt,
            IndicatorCount = dashboard.TotalIndicators,
            CollectedIndicatorCount = dashboard.CollectedIndicators,
            EvidenceCount = dashboard.EvidenceCount,
            CompletionRate = dashboard.CompletionRate,
            CreationTime = entity.CreationTime,
            CreatorId = entity.CreatorId,
            LastModificationTime = entity.LastModificationTime,
            LastModifierId = entity.LastModifierId
        };
    }

    private async Task<List<DoubleHighProjectDto>> MapProjectDtosAsync(List<DoubleHighProject> entities)
    {
        var result = new List<DoubleHighProjectDto>();
        foreach (var entity in entities)
        {
            result.Add(await MapProjectDtoAsync(entity));
        }

        return result;
    }

    private async Task<DoubleHighProjectDetailDto> MapProjectDetailDtoAsync(DoubleHighProject entity)
    {
        var projectDto = await MapProjectDtoAsync(entity);
        var indicators = await _indicatorRepository.GetListAsync(x => x.ProjectId == entity.Id);
        var evidences = await _evidenceRepository.GetListAsync(x => x.ProjectId == entity.Id);
        var reports = await _reportRepository.GetListAsync(x => x.ProjectId == entity.Id);
        var latestValues = await GetLatestValueMapAsync(indicators.Select(x => x.Id).Distinct().ToList());

        return new DoubleHighProjectDetailDto
        {
            Id = projectDto.Id,
            Title = projectDto.Title,
            BatchCode = projectDto.BatchCode,
            Description = projectDto.Description,
            Status = projectDto.Status,
            StartTime = projectDto.StartTime,
            EndTime = projectDto.EndTime,
            LastCollectedAt = projectDto.LastCollectedAt,
            IndicatorCount = projectDto.IndicatorCount,
            CollectedIndicatorCount = projectDto.CollectedIndicatorCount,
            EvidenceCount = projectDto.EvidenceCount,
            CompletionRate = projectDto.CompletionRate,
            CreationTime = projectDto.CreationTime,
            CreatorId = projectDto.CreatorId,
            LastModificationTime = projectDto.LastModificationTime,
            LastModifierId = projectDto.LastModifierId,
            Dashboard = await BuildDashboardAsync(entity),
            Indicators = indicators
                .OrderBy(x => x.CategoryName)
                .ThenBy(x => x.SortOrder)
                .Select(x => new DoubleHighIndicatorDto
                {
                    Id = x.Id,
                    ProjectId = x.ProjectId,
                    ParentId = x.ParentId,
                    CategoryName = x.CategoryName,
                    IndicatorCode = x.IndicatorCode,
                    Name = x.Name,
                    Description = x.Description,
                    Unit = x.Unit,
                    DataSourceType = x.DataSourceType,
                    TargetValue = x.TargetValue,
                    Weight = x.Weight,
                    SortOrder = x.SortOrder,
                    LatestValue = latestValues.TryGetValue(x.Id, out var latestValue)
                        ? MapValueSnapshot(latestValue)
                        : null
                })
                .ToList(),
            Evidences = await MapEvidenceDtosAsync(evidences),
            RecentReports = (await MapReportDtosAsync(reports.OrderByDescending(x => x.GeneratedAt).Take(10).ToList()))
        };
    }

    private static DoubleHighIndicatorValueSnapshotDto MapValueSnapshot(DoubleHighIndicatorValue entity)
    {
        return new DoubleHighIndicatorValueSnapshotDto
        {
            IndicatorId = entity.IndicatorId,
            Value = entity.Value,
            Note = entity.Note,
            SourceType = entity.SourceType,
            CollectedAt = entity.CollectedAt
        };
    }

    private async Task<DoubleHighEvidenceDto> MapEvidenceDtoAsync(DoubleHighEvidence entity)
    {
        return (await MapEvidenceDtosAsync(new List<DoubleHighEvidence> { entity })).First();
    }

    private async Task<List<DoubleHighEvidenceDto>> MapEvidenceDtosAsync(List<DoubleHighEvidence> entities)
    {
        if (entities.Count == 0)
        {
            return new List<DoubleHighEvidenceDto>();
        }

        var indicatorIds = entities.Select(x => x.IndicatorId).Distinct().ToList();
        var resourceIds = entities.Where(x => x.ResourceId.HasValue).Select(x => x.ResourceId!.Value).Distinct().ToList();

        var indicatorQuery = await _indicatorRepository.GetQueryableAsync();
        var indicatorMap = (await indicatorQuery.Where(x => indicatorIds.Contains(x.Id)).ToListAsync())
            .ToDictionary(x => x.Id, x => x.Name);

        Dictionary<Guid, string> resourceMap;
        using (DataFilter.Disable<IMultiTenant>())
        {
            if (resourceIds.Count == 0)
            {
                resourceMap = new Dictionary<Guid, string>();
            }
            else
            {
                var resourceQuery = await _resourceRepository.GetQueryableAsync();
                resourceMap = (await resourceQuery.Where(x => resourceIds.Contains(x.Id)).ToListAsync())
                    .ToDictionary(x => x.Id, x => x.Name);
            }
        }

        return entities
            .OrderBy(x => x.SortOrder)
            .ThenByDescending(x => x.CreationTime)
            .Select(x => new DoubleHighEvidenceDto
            {
                Id = x.Id,
                ProjectId = x.ProjectId,
                IndicatorId = x.IndicatorId,
                IndicatorName = indicatorMap.GetValueOrDefault(x.IndicatorId, string.Empty),
                Title = x.Title,
                Description = x.Description,
                EvidenceType = x.EvidenceType,
                ResourceId = x.ResourceId,
                ResourceName = x.ResourceId.HasValue ? resourceMap.GetValueOrDefault(x.ResourceId.Value, string.Empty) : null,
                AttachmentUrl = x.AttachmentUrl,
                ExternalLink = x.ExternalLink,
                SortOrder = x.SortOrder,
                CreationTime = x.CreationTime,
                CreatorId = x.CreatorId,
                LastModificationTime = x.LastModificationTime,
                LastModifierId = x.LastModifierId
            })
            .ToList();
    }

    private async Task<List<DoubleHighReportDto>> MapReportDtosAsync(List<DoubleHighReport> entities)
    {
        if (entities.Count == 0)
        {
            return new List<DoubleHighReportDto>();
        }

        var projectIds = entities.Select(x => x.ProjectId).Distinct().ToList();
        var userIds = entities.Where(x => x.GeneratedById.HasValue).Select(x => x.GeneratedById!.Value).Distinct().ToList();

        var projectQuery = await _projectRepository.GetQueryableAsync();
        var projectMap = (await projectQuery.Where(x => projectIds.Contains(x.Id)).ToListAsync())
            .ToDictionary(x => x.Id, x => x.Title);

        Dictionary<Guid, string> userMap;
        using (DataFilter.Disable<IMultiTenant>())
        {
            if (userIds.Count == 0)
            {
                userMap = new Dictionary<Guid, string>();
            }
            else
            {
                var userQuery = await _userRepository.GetQueryableAsync();
                userMap = (await userQuery.Where(x => userIds.Contains(x.Id)).ToListAsync())
                    .ToDictionary(x => x.Id, x => string.IsNullOrWhiteSpace(x.Name) ? x.UserName : x.Name);
            }
        }

        return entities.Select(x => new DoubleHighReportDto
        {
            Id = x.Id,
            ProjectId = x.ProjectId,
            ProjectTitle = projectMap.GetValueOrDefault(x.ProjectId, string.Empty),
            ReportName = x.ReportName,
            SummaryJson = x.SummaryJson,
            GeneratedById = x.GeneratedById,
            GeneratedByName = x.GeneratedById.HasValue ? userMap.GetValueOrDefault(x.GeneratedById.Value, string.Empty) : null,
            GeneratedAt = x.GeneratedAt,
            CreationTime = x.CreationTime,
            CreatorId = x.CreatorId,
            LastModificationTime = x.LastModificationTime,
            LastModifierId = x.LastModifierId
        }).ToList();
    }

    private static void ApplyHeaderStyle(IXLWorksheet worksheet, int row, int columnCount)
    {
        var range = worksheet.Range(row, 1, row, columnCount);
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.LightGray;
    }
}
