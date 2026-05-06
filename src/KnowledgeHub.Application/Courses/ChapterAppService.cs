using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using KnowledgeHub.Courses.Dtos;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using KnowledgeHub.Courses;

namespace KnowledgeHub.Courses;

public class ChapterAppService : ApplicationService, IChapterAppService
{
    private readonly IRepository<Chapter, Guid> _chapterRepository;

    public ChapterAppService(IRepository<Chapter, Guid> chapterRepository)
    {
        _chapterRepository = chapterRepository;
    }

    public async Task<ChapterDto> GetAsync(Guid id)
    {
        var chapter = await _chapterRepository.GetAsync(id);
        return ObjectMapper.Map<Chapter, ChapterDto>(chapter);
    }

    public async Task<PagedResultDto<ChapterDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _chapterRepository.GetQueryableAsync();
        var chapters = query.Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        
        return new PagedResultDto<ChapterDto>(
            query.Count(),
            ObjectMapper.Map<List<Chapter>, List<ChapterDto>>(chapters)
        );
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<ChapterDto> CreateAsync(CreateUpdateChapterDto input)
    {
        var chapter = new Chapter(
            GuidGenerator.Create(),
            input.CourseId,
            input.Title,
            input.SortOrder,
            input.ParentId
        );
        chapter.Description = input.Description;
        
        await _chapterRepository.InsertAsync(chapter);
        return ObjectMapper.Map<Chapter, ChapterDto>(chapter);
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<ChapterDto> UpdateAsync(Guid id, CreateUpdateChapterDto input)
    {
        var chapter = await _chapterRepository.GetAsync(id);
        chapter.Title = input.Title;
        chapter.Description = input.Description;
        chapter.SortOrder = input.SortOrder;
        chapter.SetParent(input.ParentId);
        
        await _chapterRepository.UpdateAsync(chapter);
        return ObjectMapper.Map<Chapter, ChapterDto>(chapter);
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task DeleteAsync(Guid id)
    {
        // Recursively collect all descendant IDs before deleting
        var query = await _chapterRepository.GetQueryableAsync();
        var allChapters = query.ToList();

        var idsToDelete = new List<Guid> { id };
        CollectDescendants(id, allChapters, idsToDelete);

        // Delete descendants first (bottom-up), then the target
        foreach (var childId in idsToDelete.Skip(1).AsEnumerable().Reverse())
        {
            await _chapterRepository.DeleteAsync(childId);
        }
        await _chapterRepository.DeleteAsync(id);
    }

    private void CollectDescendants(Guid parentId, List<Chapter> allChapters, List<Guid> result)
    {
        var children = allChapters.Where(c => c.ParentId == parentId).ToList();
        foreach (var child in children)
        {
            result.Add(child.Id);
            CollectDescendants(child.Id, allChapters, result);
        }
    }

    public async Task<List<ChapterDto>> GetChaptersByCourseAsync(Guid courseId)
    {
        var query = await _chapterRepository.GetQueryableAsync();
        var chapters = query.Where(c => c.CourseId == courseId).OrderBy(c => c.SortOrder).ToList();

        // Ensure navigation properties are not null before mapping
        foreach (var chapter in chapters)
        {
            chapter.Children ??= new List<Chapter>();
            chapter.KnowledgeResources ??= new List<KnowledgeResource>();
        }

        return ObjectMapper.Map<List<Chapter>, List<ChapterDto>>(chapters);
    }

    public async Task<List<ChapterDto>> GetChapterTreeAsync(Guid courseId)
    {
        var query = await _chapterRepository.GetQueryableAsync();
        var allChapters = query.Where(c => c.CourseId == courseId).OrderBy(c => c.SortOrder).ToList();

        if (allChapters.Count == 0)
        {
            return new List<ChapterDto>();
        }

        var chapterDtos = allChapters.Select(c => new ChapterDto
        {
            Id = c.Id,
            CourseId = c.CourseId,
            ParentId = c.ParentId,
            Title = c.Title,
            Description = c.Description,
            SortOrder = c.SortOrder,
            Children = new List<ChapterDto>(),
            KnowledgeResources = new List<KnowledgeResourceDto>(),
        }).ToList();

        return BuildTree(chapterDtos);
    }

    private List<ChapterDto> BuildTree(List<ChapterDto> chapters)
    {
        var lookup = chapters.ToDictionary(c => c.Id, c => c);
        var roots = new List<ChapterDto>();
        
        foreach (var chapter in chapters)
        {
            if (chapter.ParentId == null || !lookup.ContainsKey(chapter.ParentId.Value))
            {
                roots.Add(chapter);
            }
            else
            {
                var parent = lookup[chapter.ParentId.Value];
                parent.Children.Add(chapter);
            }
        }
        
        return roots;
    }

    public async Task DeleteByCourseAsync(Guid courseId)
    {
        var query = await _chapterRepository.GetQueryableAsync();
        var chapters = query.Where(c => c.CourseId == courseId).ToList();

        foreach (var chapter in chapters)
        {
            await _chapterRepository.DeleteAsync(chapter);
        }
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<ChapterImportResultDto> ImportFromExcelAsync(Guid courseId, IFormFile file)
    {
        var result = new ChapterImportResultDto();

        try
        {
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            stream.Position = 0;
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.First();

            // 获取有数据的行（跳过前两行：说明和表头）
            var rows = worksheet.RowsUsed().Skip(2).ToList();
            result.TotalRows = rows.Count;

            if (rows.Count == 0)
            {
                result.Errors.Add("Excel 文件中没有数据行");
                return result;
            }

            // 用于存储每一行对应的章节ID，按列索引（A=一级, B=二级, C=三级, D=四级, E=五级）
            var columnChapters = new Dictionary<int, Chapter>();

            // 当前 sortOrder
            var sortOrder = 0;

            foreach (var row in rows)
            {
                try
                {
                    // A-F 列分别对应 一级章节 到 五级章节 和 章节说明
                    var cellA = row.Cell(1).GetString(); // 一级章节
                    var cellB = row.Cell(2).GetString(); // 二级章节
                    var cellC = row.Cell(3).GetString(); // 三级章节
                    var cellD = row.Cell(4).GetString(); // 四级章节
                    var cellE = row.Cell(5).GetString(); // 五级章节
                    var cellF = row.Cell(6).GetString(); // 章节说明

                    // 按从右到左的顺序检查，找到最左边的非空列
                    string? title = null;
                    int level = 0;

                    if (!string.IsNullOrWhiteSpace(cellE)) { title = cellE.Trim(); level = 5; }
                    else if (!string.IsNullOrWhiteSpace(cellD)) { title = cellD.Trim(); level = 4; }
                    else if (!string.IsNullOrWhiteSpace(cellC)) { title = cellC.Trim(); level = 3; }
                    else if (!string.IsNullOrWhiteSpace(cellB)) { title = cellB.Trim(); level = 2; }
                    else if (!string.IsNullOrWhiteSpace(cellA)) { title = cellA.Trim(); level = 1; }

                    if (string.IsNullOrWhiteSpace(title))
                    {
                        continue; // 跳过空行
                    }

                    // 确定父章节
                    Guid? parentId = null;
                    if (level > 1)
                    {
                        // 查找上一级（level-1）的章节作为父章节
                        if (columnChapters.TryGetValue(level - 1, out var parentChapter))
                        {
                            parentId = parentChapter.Id;
                        }
                    }

                    // 创建章节
                    var chapter = new Chapter(
                        GuidGenerator.Create(),
                        courseId,
                        title,
                        sortOrder++,
                        parentId
                    );
                    chapter.Description = string.IsNullOrWhiteSpace(cellF) ? null : cellF.Trim();

                    await _chapterRepository.InsertAsync(chapter);

                    // 更新当前级别的章节引用
                    columnChapters[level] = chapter;

                    // 清除比当前级别更深的章节引用（当出现更高级别的章节时）
                    for (int l = level + 1; l <= 5; l++)
                    {
                        columnChapters.Remove(l);
                    }

                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    result.FailCount++;
                    result.Errors.Add($"行 {row.RowNumber()}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"解析 Excel 文件失败: {ex.Message}");
        }

        return result;
    }
}
