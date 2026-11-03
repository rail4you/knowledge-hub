using System;
using Microsoft.AspNetCore.Mvc;
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
using KnowledgeHub.Employment;
using KnowledgeHub.Employment.Enums;
using KnowledgeHub.Learning;
using KnowledgeHub.Learning.Enums;
using KnowledgeHub.Majors;
using KnowledgeHub.MicroMajors;
using KnowledgeHub.MicroMajors.Enums;
using KnowledgeHub.News;
using KnowledgeHub.News.Enums;
using KnowledgeHub.Permissions;
using KnowledgeHub.Practicums;
using KnowledgeHub.Practicums.Enums;
using KnowledgeHub.ResourceLibraries;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Content;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.DoubleHigh;

[IgnoreAntiforgeryToken]
public class DoubleHighAppService : KnowledgeHubAppService, IDoubleHighAppService
{
    private readonly IRepository<DoubleHighProject, Guid> _projectRepository;
    private readonly IRepository<DoubleHighIndicator, Guid> _indicatorRepository;
    private readonly IRepository<DoubleHighIndicatorValue, Guid> _valueRepository;
    private readonly IRepository<DoubleHighEvidence, Guid> _evidenceRepository;
    private readonly IRepository<DoubleHighProjectDocument, Guid> _documentRepository;
    private readonly IRepository<DoubleHighReport, Guid> _reportRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<MicroMajor, Guid> _microMajorRepository;
    private readonly IRepository<MicroMajorEnrollment, Guid> _microMajorEnrollmentRepository;
    private readonly IRepository<PracticumProject, Guid> _practicumProjectRepository;
    private readonly IRepository<PracticumEnrollment, Guid> _practicumEnrollmentRepository;
    private readonly IRepository<NewsArticle, Guid> _newsArticleRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IRepository<StudentCourse, Guid> _studentCourseRepository;
    private readonly IRepository<EmploymentOutcome, Guid> _employmentOutcomeRepository;
    private readonly IRepository<ResourceLibrary, Guid> _libraryRepository;
    private readonly IRepository<Major, Guid> _majorRepository;
    private readonly DoubleHighEvidenceFileStore _evidenceFileStore;
    private readonly DoubleHighPackageBuilder _packageBuilder;
    private readonly DoubleHighScopeResolver _scopeResolver;

    /// <summary>佐证文件类型白名单（文档 / 图片 / 压缩包），与资源库上传保持一致。</summary>
    private static readonly HashSet<string> AllowedEvidenceExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".txt", ".md", ".csv", ".json", ".xml",
        ".zip", ".rar", ".7z",
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"
    };

    public DoubleHighAppService(
        IRepository<DoubleHighProject, Guid> projectRepository,
        IRepository<DoubleHighIndicator, Guid> indicatorRepository,
        IRepository<DoubleHighIndicatorValue, Guid> valueRepository,
        IRepository<DoubleHighEvidence, Guid> evidenceRepository,
        IRepository<DoubleHighProjectDocument, Guid> documentRepository,
        IRepository<DoubleHighReport, Guid> reportRepository,
        IRepository<Resource, Guid> resourceRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<MicroMajor, Guid> microMajorRepository,
        IRepository<MicroMajorEnrollment, Guid> microMajorEnrollmentRepository,
        IRepository<PracticumProject, Guid> practicumProjectRepository,
        IRepository<PracticumEnrollment, Guid> practicumEnrollmentRepository,
        IRepository<NewsArticle, Guid> newsArticleRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IRepository<StudentCourse, Guid> studentCourseRepository,
        IRepository<EmploymentOutcome, Guid> employmentOutcomeRepository,
        IRepository<ResourceLibrary, Guid> libraryRepository,
        IRepository<Major, Guid> majorRepository,
        DoubleHighEvidenceFileStore evidenceFileStore,
        DoubleHighPackageBuilder packageBuilder,
        DoubleHighScopeResolver scopeResolver)
    {
        _projectRepository = projectRepository;
        _indicatorRepository = indicatorRepository;
        _valueRepository = valueRepository;
        _evidenceRepository = evidenceRepository;
        _documentRepository = documentRepository;
        _reportRepository = reportRepository;
        _resourceRepository = resourceRepository;
        _courseRepository = courseRepository;
        _microMajorRepository = microMajorRepository;
        _microMajorEnrollmentRepository = microMajorEnrollmentRepository;
        _practicumProjectRepository = practicumProjectRepository;
        _practicumEnrollmentRepository = practicumEnrollmentRepository;
        _newsArticleRepository = newsArticleRepository;
        _userRepository = userRepository;
        _studentCourseRepository = studentCourseRepository;
        _employmentOutcomeRepository = employmentOutcomeRepository;
        _libraryRepository = libraryRepository;
        _majorRepository = majorRepository;
        _evidenceFileStore = evidenceFileStore;
        _packageBuilder = packageBuilder;
        _scopeResolver = scopeResolver;
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
            EndTime = input.EndTime,
            TemplateKey = NormalizeTemplateKey(input.TemplateKey),
            AssessmentTarget = input.AssessmentTarget?.Trim(),
            ReporterName = input.ReporterName?.Trim()
        };

        await ApplyObjectAsync(entity, input.ObjectType, input.ObjectId, input.ObjectName);

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
        entity.TemplateKey = NormalizeTemplateKey(input.TemplateKey);
        entity.AssessmentTarget = input.AssessmentTarget?.Trim();
        entity.ReporterName = input.ReporterName?.Trim();
        await ApplyObjectAsync(entity, input.ObjectType, input.ObjectId, input.ObjectName);

        await _projectRepository.UpdateAsync(entity, autoSave: true);
        await ReplaceIndicatorsAsync(id, input.Indicators);
        return await MapProjectDtoAsync(entity);
    }

    /// <summary>
    /// 评估模板目录：新建评估项目时先选模板，指标 / 权重 / 评分规则 / 必备佐证一并预置。
    /// </summary>
    [Authorize(KnowledgeHubPermissions.DoubleHigh.Default)]
    public Task<List<DoubleHighTemplateDto>> GetTemplatesAsync()
    {
        return Task.FromResult(DoubleHighTemplates.GetAll());
    }

    /// <summary>
    /// 按模板创建评估项目（教师自助创建）：自动预置指标、权重与评分规则，项目处于「配置中」。
    /// </summary>
    [Authorize(KnowledgeHubPermissions.DoubleHigh.ManageProject)]
    public async Task<DoubleHighProjectDetailDto> CreateFromTemplateAsync(CreateDoubleHighProjectFromTemplateDto input)
    {
        var template = DoubleHighTemplates.Find(input.TemplateKey)
                       ?? throw new UserFriendlyException("评估模板不存在，请重新选择模板。");

        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new UserFriendlyException("评估项目名称不能为空。");
        }

        var batchCode = string.IsNullOrWhiteSpace(input.BatchCode)
            ? $"EVAL-{DateTime.Now:yyyyMMddHHmmss}"
            : input.BatchCode.Trim();

        var entity = new DoubleHighProject(GuidGenerator.Create(), input.Title.Trim(), batchCode)
        {
            TenantId = CurrentTenant.Id,
            Description = input.Description?.Trim(),
            Status = DoubleHighProjectStatus.Draft,
            StartTime = input.StartTime,
            EndTime = input.EndTime,
            TemplateKey = template.Key,
            AssessmentTarget = input.AssessmentTarget?.Trim(),
            ReporterName = input.ReporterName?.Trim()
        };

        await ApplyObjectAsync(entity, input.ObjectType, input.ObjectId, input.ObjectName);

        await _projectRepository.InsertAsync(entity, autoSave: true);

        var prefix = template.Key switch
        {
            DoubleHighTemplates.CourseTemplateKey => "C",
            DoubleHighTemplates.MajorTemplateKey => "M",
            DoubleHighTemplates.TeamTemplateKey => "T",
            _ => "I"
        };

        var order = 0;
        foreach (var item in template.Indicators)
        {
            order++;
            var indicator = new DoubleHighIndicator(
                GuidGenerator.Create(),
                entity.Id,
                $"{prefix}{order:00}",
                item.Name)
            {
                TenantId = CurrentTenant.Id,
                CategoryName = template.ScopeLabel,
                Description = null,
                Unit = item.Unit,
                DataSourceType = item.DataSourceType,
                TargetValue = item.TargetValue,
                Weight = item.Weight,
                SortOrder = order,
                IndicatorType = item.IndicatorType,
                ScoringRule = item.ScoringRule,
                RequiredEvidences = SerializeRequiredEvidences(item.RequiredEvidences),
                Basis = item.Basis,
                DataSourceName = item.DataSourceName
            };
            await _indicatorRepository.InsertAsync(indicator);
        }

        await _projectRepository.UpdateAsync(entity, autoSave: true);
        return await MapProjectDetailDtoAsync(entity);
    }

    /// <summary>
    /// 开始填报：配置定稿，指标（权重 / 评分规则）锁定，并把系统取数指标的值自动带出。
    /// </summary>
    [Authorize(KnowledgeHubPermissions.DoubleHigh.ManageProject)]
    public async Task<DoubleHighProjectDto> StartFillingAsync(Guid id)
    {
        var entity = await GetProjectEntityAsync(id);

        if (entity.Status == DoubleHighProjectStatus.Submitted)
        {
            throw new UserFriendlyException("该项目已提交，不能重新开始填报。");
        }

        if (entity.Status == DoubleHighProjectStatus.Active)
        {
            return await MapProjectDtoAsync(entity);
        }

        var indicators = await _indicatorRepository.GetListAsync(x => x.ProjectId == id);
        if (indicators.Count == 0)
        {
            throw new UserFriendlyException("请先在「项目配置」中至少配置一个指标，再开始填报。");
        }

        entity.Status = DoubleHighProjectStatus.Active;
        await _projectRepository.UpdateAsync(entity, autoSave: true);

        // 系统取数指标立即取一次数，让教师在填报页直接看到当前值。
        await CollectAutomaticValuesAsync(entity);

        return await MapProjectDtoAsync(entity);
    }

    /// <summary>
    /// 提交填报：所有指标都完成（含必备佐证齐全）才允许提交，提交后内容锁定。
    /// </summary>
    [Authorize(KnowledgeHubPermissions.DoubleHigh.CollectData)]
    public async Task<DoubleHighProjectDto> SubmitAsync(Guid id)
    {
        var entity = await GetProjectEntityAsync(id);

        if (entity.Status == DoubleHighProjectStatus.Submitted)
        {
            return await MapProjectDtoAsync(entity);
        }

        if (entity.Status != DoubleHighProjectStatus.Active)
        {
            throw new UserFriendlyException("请先点击「开始填报」再提交。");
        }

        var detail = await MapProjectDetailDtoAsync(entity);
        var pending = detail.Indicators
            .Where(x => !x.Completed)
            .Select(x => $"{DoubleHighPackageBuilder.IndicatorCode(detail.Indicators.IndexOf(x))} {x.Name}")
            .ToList();

        if (pending.Count > 0)
        {
            throw new UserFriendlyException(
                $"还有 {pending.Count} 项指标未完成填报：{string.Join("、", pending)}。请补齐后再提交。");
        }

        entity.Status = DoubleHighProjectStatus.Submitted;
        entity.SubmittedAt = DateTime.UtcNow;
        await _projectRepository.UpdateAsync(entity, autoSave: true);

        return await MapProjectDtoAsync(entity);
    }

    /// <summary>
    /// 保存教师填报内容（数值 / 说明 / 自评备注）。项目提交后不再允许修改。
    /// </summary>
    [Authorize(KnowledgeHubPermissions.DoubleHigh.CollectData)]
    public async Task<DoubleHighIndicatorDto> SaveReportingAsync(SaveDoubleHighReportingDto input)
    {
        var indicator = await _indicatorRepository.GetAsync(input.IndicatorId);
        var project = await GetProjectEntityAsync(indicator.ProjectId);

        if (project.Status == DoubleHighProjectStatus.Submitted || project.Status == DoubleHighProjectStatus.Closed)
        {
            throw new UserFriendlyException("该项目已提交填报，不能再修改内容。");
        }

        var textValue = input.TextValue?.Trim();
        var hasValue = input.Value.HasValue;

        if (indicator.IndicatorType == DoubleHighIndicatorType.Auto && hasValue)
        {
            throw new UserFriendlyException("「系统取数」指标由平台自动带出，不可手工修改。");
        }

        if (indicator.IndicatorType == DoubleHighIndicatorType.EvidenceOnly)
        {
            // 仅佐证指标没有填报内容，只需在「填报指标」页上传佐证材料。
            throw new UserFriendlyException("该指标只需上传佐证材料，无需填报内容。");
        }

        if (indicator.IndicatorType == DoubleHighIndicatorType.Number && !hasValue)
        {
            throw new UserFriendlyException("请填写该指标的数值。");
        }

        if (indicator.IndicatorType == DoubleHighIndicatorType.Text && string.IsNullOrWhiteSpace(textValue))
        {
            throw new UserFriendlyException("请填写该指标的说明内容。");
        }

        if (indicator.IndicatorType == DoubleHighIndicatorType.Number && hasValue)
        {
            var value = input.Value!.Value;
            // 只有「比例制」的目标值是上限（实际值 / 目标值 × 100）。
            // 「达标制」的目标值是下限（实际值 ≥ 目标值即满分），超过目标值是正常的；
            // 「直接计分」填的是百分制得分，也不受目标值约束。
            if (indicator.ScoringRule == DoubleHighScoringRule.Proportional
                && indicator.TargetValue.HasValue && indicator.TargetValue.Value > 0
                && value > indicator.TargetValue.Value)
            {
                throw new UserFriendlyException(
                    $"填报值 {value} 不能大于目标值 {indicator.TargetValue.Value}。请调低填报值，或先在「项目配置」中调高目标值。");
            }
        }

        // 自动采集指标：只允许补写自评说明，数值必须保留上一次采集到的值，
        // 否则这条新记录会变成「最新值」，把系统统计值盖成 0。
        decimal valueToSave;
        if (indicator.IndicatorType == DoubleHighIndicatorType.Auto)
        {
            var latestValues = await GetLatestValueMapAsync(new List<Guid> { indicator.Id });
            valueToSave = latestValues.TryGetValue(indicator.Id, out var latest) ? latest.Value : 0m;
        }
        else
        {
            valueToSave = input.Value.GetValueOrDefault();
        }

        var entity = new DoubleHighIndicatorValue(
            GuidGenerator.Create(),
            indicator.ProjectId,
            indicator.Id,
            valueToSave)
        {
            TenantId = CurrentTenant.Id,
            SourceType = DoubleHighValueSourceType.Manual,
            TextValue = string.IsNullOrWhiteSpace(textValue) ? null : textValue,
            Note = input.Note?.Trim()
        };

        await _valueRepository.InsertAsync(entity, autoSave: true);
        return await MapIndicatorDtoAsync(indicator);
    }

    /// <summary>
    /// 上传佐证文件（教师在填报页上传），文件落到对象存储 / 本地磁盘，导出资料包时一并打包。
    /// </summary>
    [Authorize(KnowledgeHubPermissions.DoubleHigh.CollectData)]
    public async Task<DoubleHighEvidenceDto> AddEvidenceFileAsync(
        Guid projectId,
        Guid indicatorId,
        string? evidenceName,
        IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            throw new UserFriendlyException("请选择要上传的佐证文件。");
        }

        if (file.Length > 50 * 1024 * 1024)
        {
            throw new UserFriendlyException("佐证文件不能超过 50MB。");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedEvidenceExtensions.Contains(extension))
        {
            throw new UserFriendlyException("不支持的文件类型，请上传文档、图片或压缩包。");
        }

        var project = await GetProjectEntityAsync(projectId);
        if (project.Status == DoubleHighProjectStatus.Submitted || project.Status == DoubleHighProjectStatus.Closed)
        {
            throw new UserFriendlyException("该项目已提交填报，不能再上传佐证材料。");
        }

        var indicator = await _indicatorRepository.GetAsync(indicatorId);
        if (indicator.ProjectId != projectId)
        {
            throw new UserFriendlyException("佐证材料所属项目和指标不匹配。");
        }

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        var content = stream.ToArray();

        var (storedPath, url) = await _evidenceFileStore.SaveAsync(
            projectId,
            file.FileName,
            file.ContentType,
            content);

        var evidenceQuery = await _evidenceRepository.GetQueryableAsync();
        var evidenceCount = await evidenceQuery.CountAsync(x => x.ProjectId == projectId);

        var entity = new DoubleHighEvidence(GuidGenerator.Create(), projectId, indicatorId, file.FileName)
        {
            TenantId = CurrentTenant.Id,
            Description = null,
            EvidenceType = DoubleHighEvidenceType.AttachmentLink,
            AttachmentUrl = string.IsNullOrWhiteSpace(url) ? null : url,
            EvidenceName = string.IsNullOrWhiteSpace(evidenceName) ? file.FileName : evidenceName.Trim(),
            FileName = file.FileName,
            FileSize = file.Length,
            StoredPath = storedPath,
            SortOrder = evidenceCount + 1
        };

        await _evidenceRepository.InsertAsync(entity, autoSave: true);
        return await MapEvidenceDtoAsync(entity);
    }

    /// <summary>
    /// 上传项目依据文件（政策文件 / 评估办法 / 通知等），可上传多个。
    /// 指标可以在「依据」里直接引用这些文件。
    /// </summary>
    [Authorize(KnowledgeHubPermissions.DoubleHigh.ManageProject)]
    public async Task<DoubleHighProjectDocumentDto> AddProjectDocumentAsync(Guid projectId, IFormFile file, string? title)
    {
        if (file == null || file.Length == 0)
        {
            throw new UserFriendlyException("请选择要上传的依据文件。");
        }

        if (file.Length > 50 * 1024 * 1024)
        {
            throw new UserFriendlyException("依据文件不能超过 50MB。");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedEvidenceExtensions.Contains(extension))
        {
            throw new UserFriendlyException("不支持的文件类型，请上传文档、图片或压缩包。");
        }

        var project = await GetProjectEntityAsync(projectId);
        if (project.Status == DoubleHighProjectStatus.Closed)
        {
            throw new UserFriendlyException("该项目已关闭，不能再上传依据文件。");
        }

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        var content = stream.ToArray();

        var (storedPath, url) = await _evidenceFileStore.SaveAsync(
            projectId,
            file.FileName,
            file.ContentType,
            content);

        var documentQuery = await _documentRepository.GetQueryableAsync();
        var count = await documentQuery.CountAsync(x => x.ProjectId == projectId);
        var entity = new DoubleHighProjectDocument(
            GuidGenerator.Create(),
            projectId,
            string.IsNullOrWhiteSpace(title) ? file.FileName : title.Trim())
        {
            TenantId = CurrentTenant.Id,
            Description = null,
            FileName = file.FileName,
            FileSize = file.Length,
            FileUrl = string.IsNullOrWhiteSpace(url) ? null : url,
            StoredPath = storedPath,
            SortOrder = count + 1
        };

        await _documentRepository.InsertAsync(entity, autoSave: true);
        return MapDocumentDto(entity);
    }

    /// <summary>删除项目依据文件（同时清掉引用它的指标依据）。</summary>
    [Authorize(KnowledgeHubPermissions.DoubleHigh.ManageProject)]
    public async Task DeleteProjectDocumentAsync(Guid documentId)
    {
        var document = await _documentRepository.GetAsync(documentId);

        // 引用了该文件的指标，依据回退为空（老师可重新手写）。
        var indicatorQuery = await _indicatorRepository.GetQueryableAsync();
        var indicators = await indicatorQuery.Where(x => x.BasisDocumentId == document.Id).ToListAsync();
        foreach (var indicator in indicators)
        {
            indicator.BasisDocumentId = null;
            await _indicatorRepository.UpdateAsync(indicator);
        }

        await _documentRepository.DeleteAsync(document, autoSave: true);
    }

    /// <summary>下载项目依据文件原件。</summary>
    [Authorize(KnowledgeHubPermissions.DoubleHigh.Default)]
    public async Task<IRemoteStreamContent> DownloadProjectDocumentAsync(Guid documentId)
    {
        var document = await _documentRepository.GetAsync(documentId);

        if (string.IsNullOrWhiteSpace(document.StoredPath))
        {
            throw new UserFriendlyException("该依据文件没有可下载的原件。");
        }

        var content = await _evidenceFileStore.ReadAsync(document.StoredPath);
        var fileName = string.IsNullOrWhiteSpace(document.FileName) ? document.Title : document.FileName!;
        return new RemoteStreamContent(new MemoryStream(content), fileName, "application/octet-stream");
    }

    private static DoubleHighProjectDocumentDto MapDocumentDto(DoubleHighProjectDocument entity)
    {
        return new DoubleHighProjectDocumentDto
        {
            Id = entity.Id,
            ProjectId = entity.ProjectId,
            Title = entity.Title,
            Description = entity.Description,
            FileName = entity.FileName,
            FileSize = entity.FileSize,
            FileUrl = entity.FileUrl,
            SortOrder = entity.SortOrder,
            CreationTime = entity.CreationTime,
            CreatorId = entity.CreatorId,
            LastModificationTime = entity.LastModificationTime,
            LastModifierId = entity.LastModifierId
        };
    }

    /// <summary>下载佐证文件（本地存储的文件通过此接口读取）。</summary>
    [Authorize(KnowledgeHubPermissions.DoubleHigh.Default)]
    public async Task<IRemoteStreamContent> DownloadEvidenceFileAsync(Guid evidenceId)
    {
        var evidence = await _evidenceRepository.GetAsync(evidenceId);

        if (string.IsNullOrWhiteSpace(evidence.StoredPath))
        {
            throw new UserFriendlyException("该佐证材料不是上传的文件，没有可下载的原件。");
        }

        var content = await _evidenceFileStore.ReadAsync(evidence.StoredPath);
        var fileName = string.IsNullOrWhiteSpace(evidence.FileName) ? evidence.Title : evidence.FileName!;
        return new RemoteStreamContent(new MemoryStream(content), fileName, "application/octet-stream");
    }

    /// <summary>
    /// 导出单个评估项目的资料包（zip）：评估数据（csv / md）+ 佐证文件原件。
    /// </summary>
    [Authorize(KnowledgeHubPermissions.DoubleHigh.ExportReport)]
    public async Task<IRemoteStreamContent> ExportPackageAsync(Guid projectId)
    {
        var detail = await GetDetailAsync(projectId);
        var storedPaths = await GetStoredPathMapAsync(detail.Evidences, detail.Documents);
        var bytes = await _packageBuilder.BuildProjectPackageAsync(detail, storedPaths);

        var fileName = $"评估资料包_{DoubleHighPackageBuilder.SanitizeFileName(detail.Title)}_{DateTime.Now:yyyyMMdd}.zip";
        return new RemoteStreamContent(new MemoryStream(bytes), fileName, "application/zip");
    }

    /// <summary>
    /// 导出全部评估项目的资料包（zip）：按项目分目录，一个 zip 包含所有项目的数据与佐证文件。
    /// </summary>
    [Authorize(KnowledgeHubPermissions.DoubleHigh.ExportReport)]
    public async Task<IRemoteStreamContent> ExportAllPackageAsync()
    {
        var details = new List<DoubleHighProjectDetailDto>();
        var storedPaths = new Dictionary<Guid, string>();

        var projects = await GetAccessibleProjectsAsync();
        foreach (var project in projects)
        {
            var detail = await MapProjectDetailDtoAsync(project);
            details.Add(detail);

            var map = await GetStoredPathMapAsync(detail.Evidences, detail.Documents);
            foreach (var pair in map)
            {
                storedPaths[pair.Key] = pair.Value;
            }
        }

        var bytes = await _packageBuilder.BuildAllPackagesAsync(details, storedPaths);
        var fileName = $"评估资料包_全部项目_{DateTime.Now:yyyyMMdd}.zip";
        return new RemoteStreamContent(new MemoryStream(bytes), fileName, "application/zip");
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

        var documents = await _documentRepository.GetListAsync(x => x.ProjectId == id);
        foreach (var document in documents)
        {
            await _documentRepository.DeleteAsync(document);
        }

        var reports = await _reportRepository.GetListAsync(x => x.ProjectId == id);
        foreach (var report in reports)
        {
            await _reportRepository.DeleteAsync(report);
        }

        await _projectRepository.DeleteAsync(id);
    }

    [Authorize(KnowledgeHubPermissions.DoubleHigh.ManageIndicator)]
    public async Task<DoubleHighIndicatorDto> AddIndicatorAsync(Guid projectId, CreateUpdateDoubleHighIndicatorDto input)
    {
        var project = await GetProjectEntityAsync(projectId);

        var categoryName = (input.CategoryName ?? string.Empty).Trim();
        var code = (input.IndicatorCode ?? string.Empty).Trim();
        var name = (input.Name ?? string.Empty).Trim();
        if (code.Length == 0 || name.Length == 0 || categoryName.Length == 0)
        {
            throw new UserFriendlyException("指标缺少必填字段：分类、编码、名称均不能为空。");
        }

        var existing = await _indicatorRepository.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IndicatorCode == code);
        if (existing != null)
        {
            throw new UserFriendlyException($"指标编码 {code} 已存在。");
        }

        // 指标数上限：单个项目最多 20 个指标。
        // 业务上两位小数权重（0.05~1.0）的实际最大指标数就是 20 个（20 × 0.05 = 1.0），
        // 加这条硬限制避免 UI 列表过长、批量计算性能退化，同时保留与“权重预算”逻辑的独立性
        // （即使将来引入更高精度的小数权重，也会被这条拦住，不会出现“指标海”）。
        var query = await _indicatorRepository.GetQueryableAsync();
        var existingIndicatorCount = await query
            .Where(x => x.ProjectId == projectId)
            .CountAsync();
        if (existingIndicatorCount >= 20)
        {
            throw new UserFriendlyException(
                $"单个项目的指标数已达上限 20，无法再添加新指标。请合并或删除部分指标后再添加。");
        }

        // 权重约束：当项目现有权重合计 ≥ 0.99（约等于上限 1），不允许再创建任何新指标。
        // 考虑两位小数权重的精度问题：3 × 0.33 = 0.99 应当视为“已满”，
        // 即便新指标权重填 0 也禁止 —— 避免出现 weight=0 的“死指标”噪声。
        // 用户必须先调整现有指标权重腾出空间。
        // 阈值选 0.99（而非 1.000001 / 0.999999）的原因：
        //   - 实际业务中两位小数权重（0.05、0.33、0.5）是主流，0.99 是三位权重加到 0.99 的临界点
        //   - 20 个指标 × 0.05 = 1.0、3 × 0.33 = 0.99 这种典型场景都能被拦住
        //   - 不会误拦 0.95（19 × 0.05，允许添加第 20 个 0.05 凑到 1.0）
        // 注：现有 ValidateWeightSumAsync 只拦 total > 1，total == 1 + newWeight == 0 这条边值会漏过，
        // 所以这里补一条专门的检查给清晰的错误文案。
        var existingWeightSum = await query
            .Where(x => x.ProjectId == projectId)
            .SumAsync(x => (decimal?)x.Weight) ?? 0;
        if (existingWeightSum >= 0.99m)
        {
            throw new UserFriendlyException(
                $"项目权重合计已接近上限（当前 {existingWeightSum:0.##}，已 ≥ 0.99），无法再添加新指标。请先调整现有指标权重腾出空间。");
        }

        // 权重语义：0~1 的小数，所有指标权重之和不超过 1（如 0.3 + 0.5 + 0.2 = 1）
        ValidateIndicatorWeight(input.Weight);
        await ValidateWeightSumAsync(projectId, input.Weight);

        var maxSortOrder = (await _indicatorRepository.GetListAsync(x => x.ProjectId == projectId))
            .DefaultIfEmpty()
            .Max(x => x?.SortOrder ?? 0);

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
            SortOrder = input.SortOrder > 0 ? input.SortOrder : maxSortOrder + 1,
            // 填报类型未指定时按数据来源推导：非手工来源 → 系统取数，手工来源 → 填写数值。
            IndicatorType = input.IndicatorType ?? DeriveIndicatorType(input.DataSourceType),
            ScoringRule = input.ScoringRule ?? DoubleHighScoringRule.Proportional,
            RequiredEvidences = SerializeRequiredEvidences(input.RequiredEvidences),
            BasisDocumentId = input.BasisDocumentId,
            ScopeType = input.ScopeType,
            ScopeId = input.ScopeId,
            ScopeName = input.ScopeName?.Trim()
        };

        await _indicatorRepository.InsertAsync(entity, autoSave: true);
        return await MapIndicatorDtoAsync(entity);
    }

    [Authorize(KnowledgeHubPermissions.DoubleHigh.ManageIndicator)]
    public async Task<DoubleHighIndicatorDto> UpdateIndicatorAsync(Guid id, CreateUpdateDoubleHighIndicatorDto input)
    {
        var entity = await _indicatorRepository.GetAsync(id);

        var categoryName = (input.CategoryName ?? string.Empty).Trim();
        var code = (input.IndicatorCode ?? string.Empty).Trim();
        var name = (input.Name ?? string.Empty).Trim();
        if (code.Length == 0 || name.Length == 0 || categoryName.Length == 0)
        {
            throw new UserFriendlyException("指标缺少必填字段：分类、编码、名称均不能为空。");
        }

        var existing = await _indicatorRepository.FirstOrDefaultAsync(
            x => x.ProjectId == entity.ProjectId && x.IndicatorCode == code && x.Id != id);
        if (existing != null)
        {
            throw new UserFriendlyException($"指标编码 {code} 已存在。");
        }

        // 权重语义：0~1 的小数，所有指标权重之和不超过 1（更新时排除自身）
        ValidateIndicatorWeight(input.Weight);
        await ValidateWeightSumAsync(entity.ProjectId, input.Weight, id);

        entity.ParentId = input.ParentId;
        entity.CategoryName = categoryName;
        entity.IndicatorCode = code;
        entity.Name = name;
        entity.Description = input.Description?.Trim();
        entity.Unit = input.Unit?.Trim();
        entity.DataSourceType = input.DataSourceType;
        entity.TargetValue = input.TargetValue;
        entity.Weight = input.Weight;
        entity.SortOrder = input.SortOrder > 0 ? input.SortOrder : entity.SortOrder;
        // 新字段为 null 时保留原有设置（兼容旧版调用方不会误清空填报类型 / 评分规则 / 必备佐证）。
        entity.IndicatorType = input.IndicatorType ?? entity.IndicatorType;
        entity.ScoringRule = input.ScoringRule ?? entity.ScoringRule;
        if (input.RequiredEvidences != null)
        {
            entity.RequiredEvidences = SerializeRequiredEvidences(input.RequiredEvidences);
        }
        entity.BasisDocumentId = input.BasisDocumentId;
        entity.ScopeType = input.ScopeType;
        entity.ScopeId = input.ScopeId;
        entity.ScopeName = input.ScopeName?.Trim();

        await _indicatorRepository.UpdateAsync(entity, autoSave: true);
        return await MapIndicatorDtoAsync(entity);
    }

    [Authorize(KnowledgeHubPermissions.DoubleHigh.ManageIndicator)]
    public async Task DeleteIndicatorAsync(Guid id)
    {
        var indicator = await _indicatorRepository.GetAsync(id);

        var values = await _valueRepository.GetListAsync(x => x.IndicatorId == id);
        foreach (var value in values)
        {
            await _valueRepository.DeleteAsync(value);
        }

        var evidences = await _evidenceRepository.GetListAsync(x => x.IndicatorId == id);
        foreach (var evidence in evidences)
        {
            await _evidenceRepository.DeleteAsync(evidence);
        }

        await _indicatorRepository.DeleteAsync(indicator, autoSave: true);
    }

    [Authorize(KnowledgeHubPermissions.DoubleHigh.ManageIndicator)]
    public async Task MoveIndicatorAsync(Guid id, bool up)
    {
        var indicator = await _indicatorRepository.GetAsync(id);
        var projectId = indicator.ProjectId;

        // 以展示顺序（仅按 SortOrder）排列，把目标指标在相邻位置间移动，
        // 再为整个项目重新分配连续的 SortOrder，确保顺序可靠且无重复值。
        var ordered = (await _indicatorRepository.GetListAsync(x => x.ProjectId == projectId))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CreationTime)
            .ToList();

        var index = ordered.FindIndex(x => x.Id == id);
        if (index < 0)
        {
            return;
        }

        var target = up ? index - 1 : index + 1;
        if (target < 0 || target >= ordered.Count)
        {
            // 已是首/末项，无法再移动
            return;
        }

        var item = ordered[index];
        ordered.RemoveAt(index);
        ordered.Insert(target, item);

        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].SortOrder = i + 1;
        }

        // GetListAsync 已跟踪所有实体；更新一项并 autoSave 会一次性持久化全部修改
        await _indicatorRepository.UpdateAsync(ordered[0], autoSave: true);
    }

    [Authorize(KnowledgeHubPermissions.DoubleHigh.ManageIndicator)]
    public async Task<decimal> GetDataSourcePreviewAsync(
        DoubleHighDataSourceType dataSourceType,
        DoubleHighObjectType? scopeType = null,
        Guid? scopeId = null)
    {
        // 表单里切换数据来源时实时预览当前租户的可统计值，前端自动填入最新值。
        // 指定了取值范围（库 / 专业 / 课程）时按范围统计。
        var scope = scopeId.HasValue
            ? new DoubleHighScope { Type = scopeType ?? DoubleHighObjectType.Manual, Id = scopeId }
            : DoubleHighScope.Unlimited;

        return await CalculateIndicatorValueAsync(CurrentTenant.Id, dataSourceType, scope);
    }

    /// <summary>
    /// 评估对象候选项（教学资源库 / 专业 / 课程）。
    /// 用于「新建评估项目」选择对象，以及手动对象下逐指标确认取值范围。
    /// </summary>
    [Authorize(KnowledgeHubPermissions.DoubleHigh.Default)]
    public async Task<List<DoubleHighObjectOptionDto>> GetObjectOptionsAsync(
        DoubleHighObjectType objectType,
        string? filter)
    {
        var keyword = filter?.Trim();

        // 与取值范围解析保持一致：关掉租户过滤器。
        // 宿主管理员（TenantId = null）也要能选到 / 显示租户的资源库、专业与课程。
        using (DataFilter.Disable<IMultiTenant>())
        {
            return await LoadObjectOptionsAsync(objectType, keyword);
        }
    }

    private async Task<List<DoubleHighObjectOptionDto>> LoadObjectOptionsAsync(
        DoubleHighObjectType objectType,
        string? keyword)
    {
        switch (objectType)
        {
            case DoubleHighObjectType.ResourceLibrary:
            {
                var query = await _libraryRepository.GetQueryableAsync();
                var items = await query
                    .WhereIf(!string.IsNullOrWhiteSpace(keyword), x => x.Name.Contains(keyword!))
                    .OrderBy(x => x.Name)
                    .Take(100)
                    .ToListAsync();

                var majorIds = items.Where(x => x.PrimaryMajorId.HasValue).Select(x => x.PrimaryMajorId!.Value).Distinct().ToList();
                var majorNames = await GetMajorNameMapAsync(majorIds);

                return items.Select(x => new DoubleHighObjectOptionDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Extra = x.PrimaryMajorId.HasValue
                        ? $"主建专业：{majorNames.GetValueOrDefault(x.PrimaryMajorId.Value, "-")}"
                        : null
                }).ToList();
            }

            case DoubleHighObjectType.Major:
            {
                var query = await _majorRepository.GetQueryableAsync();
                var items = await query
                    .WhereIf(!string.IsNullOrWhiteSpace(keyword), x => x.Name.Contains(keyword!))
                    .OrderBy(x => x.Name)
                    .Take(100)
                    .ToListAsync();

                return items.Select(x => new DoubleHighObjectOptionDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Extra = x.Code
                }).ToList();
            }

            case DoubleHighObjectType.Course:
            {
                var query = await _courseRepository.GetQueryableAsync();
                var items = await query
                    .WhereIf(!string.IsNullOrWhiteSpace(keyword), x => x.Title.Contains(keyword!))
                    .OrderBy(x => x.Title)
                    .Take(100)
                    .ToListAsync();

                var majorIds = items.Where(x => x.MajorId.HasValue).Select(x => x.MajorId!.Value).Distinct().ToList();
                var majorNames = await GetMajorNameMapAsync(majorIds);

                return items.Select(x => new DoubleHighObjectOptionDto
                {
                    Id = x.Id,
                    Name = x.Title,
                    Extra = x.MajorId.HasValue
                        ? $"专业：{majorNames.GetValueOrDefault(x.MajorId.Value, "-")}"
                        : "公共课"
                }).ToList();
            }

            default:
                return new List<DoubleHighObjectOptionDto>();
        }
    }

    private async Task<Dictionary<Guid, string>> GetMajorNameMapAsync(List<Guid> majorIds)
    {
        if (majorIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var query = await _majorRepository.GetQueryableAsync();
        var majors = await query.Where(x => majorIds.Contains(x.Id)).ToListAsync();
        return majors.ToDictionary(x => x.Id, x => x.Name);
    }

    [Authorize(KnowledgeHubPermissions.DoubleHigh.CollectData)]
    public async Task<DoubleHighDashboardDto> CollectProjectAsync(Guid projectId)
    {
        var project = await GetProjectEntityAsync(projectId);
        var indicators = await _indicatorRepository.GetListAsync(x => x.ProjectId == projectId);

        foreach (var indicator in indicators.Where(x => x.IndicatorType == DoubleHighIndicatorType.Auto))
        {
            var scope = _scopeResolver.Resolve(project, indicator);
            var value = await CalculateIndicatorValueAsync(project.TenantId, indicator.DataSourceType, scope);
            var entity = new DoubleHighIndicatorValue(GuidGenerator.Create(), projectId, indicator.Id, value)
            {
                TenantId = project.TenantId,
                SourceType = DoubleHighValueSourceType.Automatic,
                // 自动采集来源可通过 SourceType 区分，不再写入调试备注，避免垃圾数据泄漏到用户“备注”列
                Note = null
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

        // 关键约束：最新值必须 ≤ 目标值（如果目标值已设置）。
        // 业务含义：目标值是“应达到的目标”，最新值不应超出目标，
        // 否则完成率计算（latest/target）被生硬地截断到 100%，脱离实际语义。
        // 未设置目标值（null/0）的指标不限制上限，保留“待定”灵活性。
        // 注：只有「比例制」指标的目标值是上限；「达标制」的目标值是下限（实际值超过目标值反而更好），
        //     「直接计分」填的是百分制得分，两者都不受此限制。
        if (indicator.ScoringRule == DoubleHighScoringRule.Proportional
            && indicator.TargetValue.HasValue && indicator.TargetValue.Value > 0
            && input.Value > indicator.TargetValue.Value)
        {
            throw new UserFriendlyException(
                $"最新值 {input.Value} 不能大于目标值 {indicator.TargetValue.Value}。请调低最新值，或先调高目标值后再保存。");
        }

        // 自动采集指标也允许手工覆盖最新值：用户在指标编辑里填写的"最新值"应即时生效，
        // 直到下一次 CollectProjectAsync 重新自动采集。故不再拒绝非手工来源指标。

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
    public async Task<DoubleHighEvidenceDto> UpdateEvidenceAsync(Guid id, CreateDoubleHighEvidenceDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new UserFriendlyException("佐证材料标题不能为空。");
        }

        var entity = await _evidenceRepository.GetAsync(id);

        var indicator = await _indicatorRepository.GetAsync(input.IndicatorId);
        if (indicator.ProjectId != entity.ProjectId)
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

        entity.Title = input.Title.Trim();
        entity.Description = input.Description?.Trim();
        entity.IndicatorId = input.IndicatorId;
        entity.EvidenceType = input.EvidenceType;
        entity.ResourceId = input.ResourceId;
        entity.AttachmentUrl = input.AttachmentUrl?.Trim();
        entity.ExternalLink = input.ExternalLink?.Trim();
        entity.SortOrder = input.SortOrder;

        await _evidenceRepository.UpdateAsync(entity, autoSave: true);
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
        var (stream, reportName) = await BuildReportStreamAsync(projectId);

        var report = new DoubleHighReport(GuidGenerator.Create(), projectId, reportName)
        {
            TenantId = CurrentTenant.Id,
            GeneratedById = CurrentUser.Id,
            SummaryJson = JsonSerializer.Serialize((await GetDetailAsync(projectId)).Dashboard)
        };
        await _reportRepository.InsertAsync(report, autoSave: true);

        return new RemoteStreamContent(
            stream,
            reportName,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(KnowledgeHubPermissions.DoubleHigh.ExportReport)]
    public async Task<IRemoteStreamContent> DownloadReportAsync(Guid reportId)
    {
        var report = await _reportRepository.GetAsync(reportId);
        var (stream, reportName) = await BuildReportStreamAsync(report.ProjectId);

        return new RemoteStreamContent(
            stream,
            report.ReportName,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    private async Task<(MemoryStream Stream, string ReportName)> BuildReportStreamAsync(Guid projectId)
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
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Seek(0, SeekOrigin.Begin);
        return (stream, reportName);
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
    }

    private async Task ReplaceIndicatorsAsync(Guid projectId, List<CreateUpdateDoubleHighIndicatorDto> inputs)
    {
        // 权重语义：0~1 的小数，同一项目下所有指标权重之和不超过 1
        foreach (var input in inputs)
        {
            ValidateIndicatorWeight(input.Weight);
        }
        var batchSum = inputs.Sum(x => x.Weight);
        if (batchSum - 1 > 0.000001m)
        {
            throw new UserFriendlyException($"指标权重之和不能超过 1，当前为 {batchSum}。");
        }

        // 重要：这里改为「按指标编码原地更新」，而不是「全删重建」。
        // 原实现会把该项目的指标连同填报值、佐证材料一起删掉再插入，
        // 教师在「项目配置」里改一下权重，之前填报的数据就全没了。
        // 现在：编码相同的指标保留原 Id 与已有数据，只更新配置；
        //       编码不再出现的指标才真正删除（连同其填报值与佐证）。
        var dbContext = await _indicatorRepository.GetDbContextAsync();
        var existing = await dbContext.Set<DoubleHighIndicator>()
            .Where(x => x.ProjectId == projectId)
            .ToListAsync();

        var existingByCode = new Dictionary<string, DoubleHighIndicator>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in existing)
        {
            existingByCode[item.IndicatorCode] = item;
        }

        // 指标编码在 (ProjectId, IndicatorCode) 上有唯一索引。两个指标互换编码时，
        // 如果直接按新编码 UPDATE，可能在库里撞唯一键（谁先更新取决于 EF 的批处理顺序）。
        // 这里先把「保留下来的旧指标」的编码改成临时值（立即落库，绕开 EF 的批处理顺序），
        // 随后再写回最终编码，就不会撞唯一键了。
        var keptExisting = inputs
            .Select(x => (x.IndicatorCode ?? string.Empty).Trim())
            .Where(x => x.Length > 0 && existingByCode.ContainsKey(x))
            .Select(x => existingByCode[x])
            .DistinctBy(x => x.Id)
            .ToList();

        foreach (var item in keptExisting)
        {
            var tempCode = $"__tmp__{item.Id:N}";
            await dbContext.Set<DoubleHighIndicator>()
                .Where(x => x.Id == item.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IndicatorCode, tempCode));
        }

        var keptIds = new List<Guid>();
        var ordered = inputs
            .Select((input, index) => new { input, index })
            .OrderBy(x => x.input.SortOrder > 0 ? x.input.SortOrder : x.index + 1)
            .ThenBy(x => x.index)
            .ToList();

        var order = 0;
        var finalCodes = new List<string>();

        foreach (var item in ordered)
        {
            order++;

            // 关键修复：DTO 上 CategoryName / IndicatorCode / Name 标注了非空且默认 string.Empty，
            // 但 ABP 反序列化 JSON 时如果前端送来 null，仍会得到 null；此时直接 .Trim() 会
            // 抛 NullReferenceException → 500 → 前端只看到笼统"保存失败"。
            // 这里统一用空串兜底再 Trim。
            var code = (item.input.IndicatorCode ?? string.Empty).Trim();
            var name = (item.input.Name ?? string.Empty).Trim();
            var categoryName = (item.input.CategoryName ?? string.Empty).Trim();

            if (code.Length == 0 || name.Length == 0 || categoryName.Length == 0)
            {
                throw new UserFriendlyException(
                    $"指标 {order} 缺少必填字段：分类、编码、名称均不能为空。");
            }

            if (finalCodes.Any(x => string.Equals(x, code, StringComparison.OrdinalIgnoreCase)))
            {
                throw new UserFriendlyException($"指标编码 {code} 重复，请修改后再保存。");
            }

            finalCodes.Add(code);

            if (existingByCode.TryGetValue(code, out var entity))
            {
                // 编码在库里已被改成临时值（见上方说明），这里写回最终编码并强制标记为已修改，
                // 否则 EF 认为编码没变，就不会把最终编码写回库。
                entity.IndicatorCode = code;
                entity.CategoryName = categoryName;
                entity.Name = name;
                entity.Description = item.input.Description?.Trim();
                entity.Unit = item.input.Unit?.Trim();
                entity.DataSourceType = item.input.DataSourceType;
                entity.TargetValue = item.input.TargetValue;
                entity.Weight = item.input.Weight;
                entity.ParentId = item.input.ParentId;
                entity.SortOrder = order;
                entity.IndicatorType = item.input.IndicatorType ?? entity.IndicatorType;
                entity.ScoringRule = item.input.ScoringRule ?? entity.ScoringRule;
                if (item.input.RequiredEvidences != null)
                {
                    entity.RequiredEvidences = SerializeRequiredEvidences(item.input.RequiredEvidences);
                }
                entity.BasisDocumentId = item.input.BasisDocumentId;
                entity.ScopeType = item.input.ScopeType;
                entity.ScopeId = item.input.ScopeId;
                entity.ScopeName = item.input.ScopeName?.Trim();
                entity.Basis = item.input.Basis?.Trim();
                entity.DataSourceName = item.input.DataSourceName?.Trim();

                await _indicatorRepository.UpdateAsync(entity);
                dbContext.Entry(entity).Property(x => x.IndicatorCode).IsModified = true;
                keptIds.Add(entity.Id);
            }
            else
            {
                var newEntity = new DoubleHighIndicator(GuidGenerator.Create(), projectId, code, name)
                {
                    TenantId = CurrentTenant.Id,
                    ParentId = item.input.ParentId,
                    CategoryName = categoryName,
                    Description = item.input.Description?.Trim(),
                    Unit = item.input.Unit?.Trim(),
                    DataSourceType = item.input.DataSourceType,
                    TargetValue = item.input.TargetValue,
                    Weight = item.input.Weight,
                    SortOrder = order,
                    IndicatorType = item.input.IndicatorType ?? DeriveIndicatorType(item.input.DataSourceType),
                    ScoringRule = item.input.ScoringRule ?? DoubleHighScoringRule.Proportional,
                    RequiredEvidences = SerializeRequiredEvidences(item.input.RequiredEvidences),
                    Basis = item.input.Basis?.Trim(),
                    BasisDocumentId = item.input.BasisDocumentId,
                    DataSourceName = item.input.DataSourceName?.Trim(),
                    ScopeType = item.input.ScopeType,
                    ScopeId = item.input.ScopeId,
                    ScopeName = item.input.ScopeName?.Trim()
                };
                await _indicatorRepository.InsertAsync(newEntity);
                keptIds.Add(newEntity.Id);
            }
        }

        var removedIds = existing.Where(x => !keptIds.Contains(x.Id)).Select(x => x.Id).ToList();
        if (removedIds.Count > 0)
        {
            // 先删填报值与佐证（无 ProjectId，只有 IndicatorId），再删指标，否则外键报错。
            await dbContext.Set<DoubleHighIndicatorValue>()
                .Where(x => removedIds.Contains(x.IndicatorId))
                .ExecuteDeleteAsync();
            await dbContext.Set<DoubleHighEvidence>()
                .Where(x => removedIds.Contains(x.IndicatorId))
                .ExecuteDeleteAsync();
            await dbContext.Set<DoubleHighIndicator>()
                .Where(x => removedIds.Contains(x.Id))
                .ExecuteDeleteAsync();
        }
    }

    /// <summary>按数据来源推导填报类型：非手工来源为系统取数，手工来源为填写数值。</summary>
    private static DoubleHighIndicatorType DeriveIndicatorType(DoubleHighDataSourceType dataSourceType)
    {
        return dataSourceType == DoubleHighDataSourceType.Manual
            ? DoubleHighIndicatorType.Number
            : DoubleHighIndicatorType.Auto;
    }

    /// <summary>必备佐证清单序列化为 JSON 数组字符串。</summary>
    private static string? SerializeRequiredEvidences(IEnumerable<string>? evidences)
    {
        var list = (evidences ?? Enumerable.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return list.Count == 0 ? null : JsonSerializer.Serialize(list);
    }

    /// <summary>反序列化必备佐证清单（兼容历史数据里没有该字段的情况）。</summary>
    internal static List<string> DeserializeRequiredEvidences(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<string>();
        }

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json);
            return list?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList() ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// 应用「评估对象」：对象类型 + 对象 id + 名称快照。
    /// 名称优先用服务端查到的真实名称（避免前端传错），手动填写时用前端输入的名字。
    /// </summary>
    private async Task ApplyObjectAsync(
        DoubleHighProject entity,
        DoubleHighObjectType objectType,
        Guid? objectId,
        string? objectName)
    {
        entity.ObjectType = objectType;
        entity.ObjectId = null;
        entity.ObjectName = null;

        if (objectType == DoubleHighObjectType.Manual || !objectId.HasValue)
        {
            // 手动填写：对象名由老师输入（可为空）。
            entity.ObjectType = DoubleHighObjectType.Manual;
            entity.ObjectName = string.IsNullOrWhiteSpace(objectName) ? null : objectName.Trim();
            return;
        }

        var resolvedName = await _scopeResolver.ResolveNameAsync(objectType, objectId.Value);
        if (string.IsNullOrWhiteSpace(resolvedName))
        {
            throw new UserFriendlyException("选择的评估对象不存在或已被删除，请重新选择。");
        }

        entity.ObjectId = objectId.Value;
        entity.ObjectName = resolvedName;
    }

    private static string NormalizeTemplateKey(string? templateKey)
    {
        if (string.IsNullOrWhiteSpace(templateKey))
        {
            return null!;
        }

        var key = templateKey.Trim();
        return DoubleHighTemplates.Find(key)?.Key ?? DoubleHighTemplates.CustomTemplateKey;
    }

    private static void ValidateIndicatorWeight(decimal weight)
    {
        // 权重语义：0~1 的小数（如 0.3），所有指标权重之和为 1；0 表示暂未分配
        if (weight < 0 || weight > 1)
        {
            throw new UserFriendlyException($"指标权重必须在 0~1 之间，当前为 {weight}。");
        }
    }

    private async Task ValidateWeightSumAsync(Guid projectId, decimal newWeight, Guid? excludeIndicatorId = null)
    {
        var query = await _indicatorRepository.GetQueryableAsync();
        var otherSum = await query
            .Where(x => x.ProjectId == projectId && (!excludeIndicatorId.HasValue || x.Id != excludeIndicatorId.Value))
            .SumAsync(x => (decimal?)x.Weight) ?? 0;
        var total = otherSum + newWeight;
        if (total - 1 > 0.000001m)
        {
            var maxAllowed = 1 - otherSum;
            if (maxAllowed < 0)
            {
                maxAllowed = 0;
            }
            throw new UserFriendlyException(
                $"指标权重之和不能超过 1：已有权重合计 {otherSum}，本次最多可填 {maxAllowed}，当前合计将为 {total}。");
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

    /// <summary>获取当前用户可见的全部项目（导出全部资料包用）。</summary>
    private async Task<List<DoubleHighProject>> GetAccessibleProjectsAsync()
    {
        if (await CanViewAllAsync())
        {
            using (DataFilter.Disable<IMultiTenant>())
            {
                return await _projectRepository.GetListAsync();
            }
        }

        return await _projectRepository.GetListAsync();
    }

    /// <summary>
    /// 自动采集「系统取数」指标的值（开始填报 / 手动刷新时调用）。
    /// </summary>
    private async Task CollectAutomaticValuesAsync(DoubleHighProject project)
    {
        var indicators = await _indicatorRepository.GetListAsync(x => x.ProjectId == project.Id);

        foreach (var indicator in indicators.Where(x => x.IndicatorType == DoubleHighIndicatorType.Auto))
        {
            var scope = _scopeResolver.Resolve(project, indicator);
            var value = await CalculateIndicatorValueAsync(project.TenantId, indicator.DataSourceType, scope);
            var entity = new DoubleHighIndicatorValue(GuidGenerator.Create(), project.Id, indicator.Id, value)
            {
                TenantId = project.TenantId,
                SourceType = DoubleHighValueSourceType.Automatic,
                Note = null
            };
            await _valueRepository.InsertAsync(entity);
        }

        project.LastCollectedAt = DateTime.UtcNow;
        await _projectRepository.UpdateAsync(project, autoSave: true);
    }

    /// <summary>取佐证文件的存储路径（导出资料包时按此路径读取原件）。</summary>
    private async Task<Dictionary<Guid, string>> GetStoredPathMapAsync(List<DoubleHighEvidenceDto> evidences)
    {
        var ids = evidences.Where(x => x.IsFile).Select(x => x.Id).ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var query = await _evidenceRepository.GetQueryableAsync();
        var items = await query.Where(x => ids.Contains(x.Id)).ToListAsync();
        return items
            .Where(x => !string.IsNullOrWhiteSpace(x.StoredPath))
            .ToDictionary(x => x.Id, x => x.StoredPath!);
    }

    /// <summary>取佐证文件 + 项目依据文件的存储路径（导出资料包时按此路径读取原件）。</summary>
    private async Task<Dictionary<Guid, string>> GetStoredPathMapAsync(
        List<DoubleHighEvidenceDto> evidences,
        List<DoubleHighProjectDocumentDto>? documents = null)
    {
        var map = await GetStoredPathMapAsync(evidences);

        if (documents is { Count: > 0 })
        {
            var documentQuery = await _documentRepository.GetQueryableAsync();
            var items = await documentQuery
                .Where(x => documents.Select(d => d.Id).Contains(x.Id))
                .ToListAsync();

            foreach (var item in items.Where(x => !string.IsNullOrWhiteSpace(x.StoredPath)))
            {
                map[item.Id] = item.StoredPath!;
            }
        }

        return map;
    }

    private async Task<decimal> CalculateIndicatorValueAsync(
        Guid? tenantId,
        DoubleHighDataSourceType dataSourceType,
        DoubleHighScope? scope = null)
    {
        scope ??= DoubleHighScope.Unlimited;

        return dataSourceType switch
        {
            DoubleHighDataSourceType.ResourceCount => await CountResourcesAsync(tenantId, scope),
            DoubleHighDataSourceType.CourseCount => await CountCoursesAsync(tenantId, scope),
            DoubleHighDataSourceType.MicroMajorCount => await CountMicroMajorsAsync(tenantId),
            DoubleHighDataSourceType.PracticumProjectCount => await CountPracticumProjectsAsync(tenantId, scope),
            DoubleHighDataSourceType.NewsArticleCount => await CountNewsArticlesAsync(tenantId),
            DoubleHighDataSourceType.MicroMajorEnrollmentCount => await CountMicroMajorEnrollmentsAsync(tenantId),
            DoubleHighDataSourceType.PracticumEnrollmentCount => await CountPracticumEnrollmentsAsync(tenantId, scope),
            DoubleHighDataSourceType.CourseEnrollmentCount => await CountCourseEnrollmentsAsync(tenantId, scope),
            DoubleHighDataSourceType.CourseCompletionRate => await GetCourseCompletionRateAsync(tenantId, scope),
            DoubleHighDataSourceType.EmploymentRate => await GetEmploymentRateAsync(tenantId, scope),
            _ => 0
        };
    }

    /// <summary>范围内课程 id；null = 不限定（按租户全部）。</summary>
    private async Task<List<Guid>?> GetScopeCourseIdsAsync(DoubleHighScope scope)
    {
        return await _scopeResolver.GetCourseIdsAsync(scope);
    }

    /// <summary>课程选课人次（排除已退课记录）。</summary>
    private async Task<decimal> CountCourseEnrollmentsAsync(Guid? tenantId, DoubleHighScope scope)
    {
        var scopeCourseIds = await _scopeResolver.GetCourseIdsAsync(scope);

        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _studentCourseRepository.GetQueryableAsync();
            return await query
                .WhereIf(scopeCourseIds == null && tenantId.HasValue, x => x.TenantId == tenantId.Value)
                .WhereIf(scopeCourseIds != null, x => scopeCourseIds.Contains(x.CourseId))
                .CountAsync(x => x.Status != StudentCourseStatus.Dropped);
        }
    }

    /// <summary>课程学习完成率（%）：范围内已完成人数 / 范围内有效选课人数。</summary>
    private async Task<decimal> GetCourseCompletionRateAsync(Guid? tenantId, DoubleHighScope scope)
    {
        var scopeCourseIds = await _scopeResolver.GetCourseIdsAsync(scope);

        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _studentCourseRepository.GetQueryableAsync();
            query = query
                .WhereIf(scopeCourseIds == null && tenantId.HasValue, x => x.TenantId == tenantId.Value)
                .WhereIf(scopeCourseIds != null, x => scopeCourseIds.Contains(x.CourseId))
                .Where(x => x.Status != StudentCourseStatus.Dropped);

            var total = await query.CountAsync();
            if (total == 0)
            {
                return 0;
            }

            var completed = await query.CountAsync(x => x.Status == StudentCourseStatus.Completed);
            return Math.Round((decimal)completed / total * 100m, 1);
        }
    }

    /// <summary>
    /// 就业率（%）：已落实就业（签约 / 就业 / 升学 / 创业）的人数 / 有就业去向记录的人数。
    /// 说明：平台没有独立的「毕业生名册」，这里以「已登记就业去向的人数」为分母；
    /// 限定范围时只统计该范围课程下的学生。
    /// </summary>
    private async Task<decimal> GetEmploymentRateAsync(Guid? tenantId, DoubleHighScope scope)
    {
        var scopeStudentIds = await _scopeResolver.GetStudentIdsAsync(scope);

        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _employmentOutcomeRepository.GetQueryableAsync();
            query = query
                .WhereIf(scopeStudentIds == null && tenantId.HasValue, x => x.TenantId == tenantId.Value)
                .WhereIf(scopeStudentIds != null, x => scopeStudentIds.Contains(x.StudentId));

            var total = await query.CountAsync();
            if (total == 0)
            {
                return 0;
            }

            var employed = await query.CountAsync(x =>
                x.Status == EmploymentOutcomeStatus.Signed ||
                x.Status == EmploymentOutcomeStatus.Employed ||
                x.Status == EmploymentOutcomeStatus.FurtherStudy ||
                x.Status == EmploymentOutcomeStatus.Entrepreneurship);

            return Math.Round((decimal)employed / total * 100m, 1);
        }
    }

    /// <summary>
    /// 范围内的资源数。
    /// 不限定范围时按租户统计；限定范围时按范围集合统计
    /// （资源库可能包含共建学校内容，因此不再叠加租户过滤）。
    /// </summary>
    private async Task<decimal> CountResourcesAsync(Guid? tenantId, DoubleHighScope scope)
    {
        var scopeIds = await _scopeResolver.GetResourceIdsAsync(scope);

        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _resourceRepository.GetQueryableAsync();
            return await query
                .WhereIf(scopeIds == null && tenantId.HasValue, x => x.TenantId == tenantId.Value)
                .WhereIf(scopeIds != null, x => scopeIds.Contains(x.Id))
                .CountAsync(x => x.Status == ResourceStatus.SchoolApproved || x.Status == ResourceStatus.LeagueApproved);
        }
    }

    /// <summary>范围内的课程数（已发布）。</summary>
    private async Task<decimal> CountCoursesAsync(Guid? tenantId, DoubleHighScope scope)
    {
        var scopeIds = await _scopeResolver.GetCourseIdsAsync(scope);

        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _courseRepository.GetQueryableAsync();
            return await query
                .WhereIf(scopeIds == null && tenantId.HasValue, x => x.TenantId == tenantId.Value)
                .WhereIf(scopeIds != null, x => scopeIds.Contains(x.Id))
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

    /// <summary>范围内的实训项目数（按所属课程）。</summary>
    private async Task<decimal> CountPracticumProjectsAsync(Guid? tenantId, DoubleHighScope scope)
    {
        var scopeCourseIds = await _scopeResolver.GetCourseIdsAsync(scope);

        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _practicumProjectRepository.GetQueryableAsync();
            return await query
                .WhereIf(scopeCourseIds == null && tenantId.HasValue, x => x.TenantId == tenantId.Value)
                .WhereIf(scopeCourseIds != null, x => x.CourseId.HasValue && scopeCourseIds.Contains(x.CourseId.Value))
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

    /// <summary>范围内的实训参与量（按范围课程下的实训项目）。</summary>
    private async Task<decimal> CountPracticumEnrollmentsAsync(Guid? tenantId, DoubleHighScope scope)
    {
        var scopeCourseIds = await _scopeResolver.GetCourseIdsAsync(scope);

        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _practicumEnrollmentRepository.GetQueryableAsync();
            if (scopeCourseIds != null)
            {
                var practicumQuery = await _practicumProjectRepository.GetQueryableAsync();
                var practicumIds = await practicumQuery
                    .Where(x => x.CourseId.HasValue && scopeCourseIds.Contains(x.CourseId.Value))
                    .Select(x => x.Id)
                    .ToListAsync();

                return await query
                    .Where(x => practicumIds.Contains(x.ProjectId))
                    .CountAsync(x => x.Status != PracticumEnrollmentStatus.Cancelled);
            }

            return await query
                .WhereIf(tenantId.HasValue, x => x.TenantId == tenantId.Value)
                .CountAsync(x => x.Status != PracticumEnrollmentStatus.Cancelled);
        }
    }

    private async Task<DoubleHighDashboardDto> BuildDashboardAsync(DoubleHighProject project)
    {
        var indicators = await _indicatorRepository.GetListAsync(x => x.ProjectId == project.Id);
        var indicatorIds = indicators.Select(x => x.Id).ToList();
        var evidences = await _evidenceRepository.GetListAsync(x => x.ProjectId == project.Id);
        var latestValues = await GetLatestValueMapAsync(indicatorIds);
        var collectedCount = indicatorIds.Count(id => latestValues.ContainsKey(id));

        var progressMap = indicators.ToDictionary(x => x.Id, x => CalculateIndicatorProgress(x, latestValues));

        // 关键修复：项目完成率 = 各指标完成度按权重加权求和（与详情页 getIndicatorProgress 公式保持一致）
        // 原实现只用 collectedCount / indicators.Count，仅反映"是否已采集"，完全不看离目标多远。
        // 举例：3 个指标里有 2 个仅采集到最新值（3% / 8%），旧逻辑会算成 66.7%；
        // 新逻辑按权重加权求和，得到的就是真实的整体进度（如 4.9%）。
        // 当所有指标权重合计 = 1 时，加权求和等价于加权平均，结果天然落在 0~100%。
        // 兜底：所有权重都为 0 时（新建项目未分配权重），按算术平均计算，避免分母为 0。
        decimal completionRate = 0;
        if (indicators.Count > 0)
        {
            decimal totalWeight = indicators.Sum(x => x.Weight);
            if (totalWeight > 0)
            {
                decimal weightedProgress = 0;
                foreach (var indicator in indicators)
                {
                    var progress = progressMap[indicator.Id];
                    weightedProgress += progress * indicator.Weight;
                }
                completionRate = Math.Round(weightedProgress / totalWeight, 1);
            }
            else
            {
                // 所有指标都未分配权重（weight = 0）：按算术平均展示真实进度，避免被 0 权重抹平
                decimal sumProgress = 0;
                foreach (var indicator in indicators)
                {
                    sumProgress += progressMap[indicator.Id];
                }
                completionRate = Math.Round(sumProgress / indicators.Count, 1);
            }
        }

        // 加权总分：只统计参与计分的指标（评分规则 ≠ 不计分），仍用「权重 × 完成度」加权求和。
        var scoredIndicators = indicators.Where(x => x.ScoringRule != DoubleHighScoringRule.None).ToList();
        decimal totalScore = 0;
        var scoredWeight = scoredIndicators.Sum(x => x.Weight);
        if (scoredIndicators.Count > 0 && scoredWeight > 0)
        {
            totalScore = Math.Round(
                scoredIndicators.Sum(x => progressMap[x.Id] * x.Weight) / scoredWeight,
                1);
        }

        // 已完成指标数：逐项判定（填报内容 + 必备佐证齐全）。
        var completedCount = indicators.Count(x =>
            IsIndicatorCompleted(x, latestValues.GetValueOrDefault(x.Id), evidences.Where(e => e.IndicatorId == x.Id).ToList()));

        return new DoubleHighDashboardDto
        {
            TotalIndicators = indicators.Count,
            ManualIndicators = indicators.Count(x => x.DataSourceType == DoubleHighDataSourceType.Manual),
            AutomaticIndicators = indicators.Count(x => x.DataSourceType != DoubleHighDataSourceType.Manual),
            CollectedIndicators = collectedCount,
            CompletedIndicators = completedCount,
            EvidenceCount = evidences.Count,
            CompletionRate = completionRate,
            TotalScore = totalScore,
            LastCollectedAt = project.LastCollectedAt
        };
    }

    /// <summary>
    /// 指标是否已完成：填报内容到位 + 必备佐证齐全。
    /// </summary>
    private static bool IsIndicatorCompleted(
        DoubleHighIndicator indicator,
        DoubleHighIndicatorValue? latest,
        List<DoubleHighEvidence> evidences)
    {
        foreach (var name in DeserializeRequiredEvidences(indicator.RequiredEvidences))
        {
            if (!evidences.Any(e => string.Equals(e.EvidenceName, name, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        return indicator.IndicatorType switch
        {
            DoubleHighIndicatorType.Auto => latest != null,
            DoubleHighIndicatorType.Number => latest != null,
            DoubleHighIndicatorType.Text => latest != null && !string.IsNullOrWhiteSpace(latest.TextValue),
            DoubleHighIndicatorType.EvidenceOnly => evidences.Count > 0,
            _ => latest != null
        };
    }

    /// <summary>指标完成状态文案。</summary>
    private static string GetCompletionStatus(
        DoubleHighIndicator indicator,
        DoubleHighIndicatorValue? latest,
        List<DoubleHighEvidence> evidences)
    {
        if (IsIndicatorCompleted(indicator, latest, evidences))
        {
            return "已完成";
        }

        foreach (var name in DeserializeRequiredEvidences(indicator.RequiredEvidences))
        {
            if (!evidences.Any(e => string.Equals(e.EvidenceName, name, StringComparison.OrdinalIgnoreCase)))
            {
                return "待补佐证";
            }
        }

        return "待填写";
    }

    private static decimal CalculateIndicatorProgress(
        DoubleHighIndicator indicator,
        Dictionary<Guid, DoubleHighIndicatorValue> latestValues)
    {
        // 完成度（0~100）按指标的评分规则折算：
        //   比例制：min(100, 实际值 / 目标值 × 100)（与历史公式一致）
        //   达标制：实际值 ≥ 目标值得 100 分，否则 0 分
        //   直接计分：教师填的值本身就是百分制得分
        //   不计分：始终为 0（不参与加权总分）
        if (!latestValues.TryGetValue(indicator.Id, out var latest))
        {
            return 0;
        }

        var target = indicator.TargetValue;
        switch (indicator.ScoringRule)
        {
            case DoubleHighScoringRule.None:
                return 0;

            case DoubleHighScoringRule.Direct:
                return Math.Round(Math.Clamp(latest.Value, 0m, 100m), 1);

            case DoubleHighScoringRule.Threshold:
                if (target == null || target <= 0)
                {
                    return 0;
                }

                return latest.Value >= target.Value ? 100m : 0m;

            case DoubleHighScoringRule.Proportional:
            default:
                if (target == null || target <= 0)
                {
                    return 0;
                }

                var raw = latest.Value / target.Value * 100m;
                if (raw > 100m)
                {
                    raw = 100m;
                }

                return Math.Round(raw, 1);
        }
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
            TotalScore = dashboard.TotalScore,
            CompletedIndicatorCount = dashboard.CompletedIndicators,
            TemplateKey = entity.TemplateKey,
            TemplateName = DoubleHighTemplates.GetName(entity.TemplateKey),
            AssessmentTarget = entity.AssessmentTarget,
            ReporterName = entity.ReporterName,
            SubmittedAt = entity.SubmittedAt,
            ObjectType = entity.ObjectType,
            ObjectId = entity.ObjectId,
            ObjectName = entity.ObjectName,
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
        var documents = await _documentRepository.GetListAsync(x => x.ProjectId == entity.Id);
        var latestValues = await GetLatestValueMapAsync(indicators.Select(x => x.Id).Distinct().ToList());

        // 指标依据可能引用项目里的依据文件，这里先建好映射供指标 DTO 使用。
        var documentMap = documents.ToDictionary(x => x.Id, x => x.Title);
        var documentUrlMap = documents
            .Where(x => !string.IsNullOrWhiteSpace(x.FileUrl))
            .ToDictionary(x => x.Id, x => x.FileUrl!);

        var evidenceGroups = evidences
            .GroupBy(x => x.IndicatorId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var indicatorDtos = indicators
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CreationTime)
            .Select(x =>
            {
                var latestValue = latestValues.GetValueOrDefault(x.Id);
                var indicatorEvidences = evidenceGroups.GetValueOrDefault(x.Id) ?? new List<DoubleHighEvidence>();
                var progress = CalculateIndicatorProgress(x, latestValues);

                return new DoubleHighIndicatorDto
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
                    LatestValue = latestValue != null ? MapValueSnapshot(latestValue) : null,
                    IndicatorType = x.IndicatorType,
                    ScoringRule = x.ScoringRule,
                    RequiredEvidences = DeserializeRequiredEvidences(x.RequiredEvidences),
                    Basis = x.Basis,
                    BasisDocumentId = x.BasisDocumentId,
                    BasisDocumentTitle = documentMap.GetValueOrDefault(x.BasisDocumentId ?? Guid.Empty),
                    BasisDocumentUrl = documentUrlMap.GetValueOrDefault(x.BasisDocumentId ?? Guid.Empty),
                    DataSourceName = x.DataSourceName,
                    ScopeType = x.ScopeType,
                    ScopeId = x.ScopeId,
                    ScopeName = x.ScopeName,
                    ScopeLabel = _scopeResolver.Describe(_scopeResolver.Resolve(entity, x)),
                    Progress = progress,
                    Score = Math.Round(progress * x.Weight, 2),
                    Completed = IsIndicatorCompleted(x, latestValue, indicatorEvidences),
                    CompletionStatus = GetCompletionStatus(x, latestValue, indicatorEvidences),
                    EvidenceCount = indicatorEvidences.Count
                };
            })
            .ToList();

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
            TotalScore = projectDto.TotalScore,
            CompletedIndicatorCount = projectDto.CompletedIndicatorCount,
            TemplateKey = projectDto.TemplateKey,
            TemplateName = projectDto.TemplateName,
            AssessmentTarget = projectDto.AssessmentTarget,
            ReporterName = projectDto.ReporterName,
            SubmittedAt = projectDto.SubmittedAt,
            ObjectType = projectDto.ObjectType,
            ObjectId = projectDto.ObjectId,
            ObjectName = projectDto.ObjectName,
            CreationTime = projectDto.CreationTime,
            CreatorId = projectDto.CreatorId,
            LastModificationTime = projectDto.LastModificationTime,
            LastModifierId = projectDto.LastModifierId,
            Dashboard = await BuildDashboardAsync(entity),
            Indicators = indicatorDtos,
            Evidences = await MapEvidenceDtosAsync(evidences),
            Documents = documents.Select(MapDocumentDto).ToList(),
            RecentReports = (await MapReportDtosAsync(reports.OrderByDescending(x => x.GeneratedAt).Take(10).ToList()))
        };
    }

    /// <summary>
    /// 解析指标的实际取值范围文案（单指标接口用，需要先拿到项目）。
    /// </summary>
    private async Task<string> ResolveScopeLabelAsync(DoubleHighIndicator indicator)
    {
        var project = await GetProjectEntityAsync(indicator.ProjectId);
        return _scopeResolver.Describe(_scopeResolver.Resolve(project, indicator));
    }

    /// <summary>取依据文件标题。</summary>
    private async Task<string?> GetDocumentTitleAsync(Guid? documentId)
    {
        if (!documentId.HasValue)
        {
            return null;
        }

        var document = await _documentRepository.FirstOrDefaultAsync(x => x.Id == documentId.Value);
        return document?.Title;
    }

    /// <summary>取依据文件公开地址（本地存储时为空，前端走下载接口）。</summary>
    private async Task<string?> GetDocumentUrlAsync(Guid? documentId)
    {
        if (!documentId.HasValue)
        {
            return null;
        }

        var document = await _documentRepository.FirstOrDefaultAsync(x => x.Id == documentId.Value);
        return document?.FileUrl;
    }

    private static DoubleHighIndicatorValueSnapshotDto MapValueSnapshot(DoubleHighIndicatorValue entity)
    {
        return new DoubleHighIndicatorValueSnapshotDto
        {
            IndicatorId = entity.IndicatorId,
            Value = entity.Value,
            TextValue = entity.TextValue,
            Note = entity.Note,
            SourceType = entity.SourceType,
            CollectedAt = entity.CollectedAt
        };
    }

    private async Task<DoubleHighIndicatorDto> MapIndicatorDtoAsync(DoubleHighIndicator entity)
    {
        var latestValues = await GetLatestValueMapAsync(new List<Guid> { entity.Id });
        var latestValue = latestValues.GetValueOrDefault(entity.Id);
        var evidences = await _evidenceRepository.GetListAsync(x => x.IndicatorId == entity.Id);
        var progress = CalculateIndicatorProgress(entity, latestValues);

        return new DoubleHighIndicatorDto
        {
            Id = entity.Id,
            ProjectId = entity.ProjectId,
            ParentId = entity.ParentId,
            CategoryName = entity.CategoryName,
            IndicatorCode = entity.IndicatorCode,
            Name = entity.Name,
            Description = entity.Description,
            Unit = entity.Unit,
            DataSourceType = entity.DataSourceType,
            TargetValue = entity.TargetValue,
            Weight = entity.Weight,
            SortOrder = entity.SortOrder,
            LatestValue = latestValue != null ? MapValueSnapshot(latestValue) : null,
            IndicatorType = entity.IndicatorType,
            ScoringRule = entity.ScoringRule,
            RequiredEvidences = DeserializeRequiredEvidences(entity.RequiredEvidences),
            Basis = entity.Basis,
            BasisDocumentId = entity.BasisDocumentId,
            BasisDocumentTitle = await GetDocumentTitleAsync(entity.BasisDocumentId),
            BasisDocumentUrl = await GetDocumentUrlAsync(entity.BasisDocumentId),
            DataSourceName = entity.DataSourceName,
            ScopeType = entity.ScopeType,
            ScopeId = entity.ScopeId,
            ScopeName = entity.ScopeName,
            ScopeLabel = await ResolveScopeLabelAsync(entity),
            Progress = progress,
            Score = Math.Round(progress * entity.Weight, 2),
            Completed = IsIndicatorCompleted(entity, latestValue, evidences),
            CompletionStatus = GetCompletionStatus(entity, latestValue, evidences),
            EvidenceCount = evidences.Count
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
                EvidenceName = x.EvidenceName,
                FileName = x.FileName,
                FileSize = x.FileSize,
                IsFile = !string.IsNullOrWhiteSpace(x.StoredPath) || !string.IsNullOrWhiteSpace(x.FileName),
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
