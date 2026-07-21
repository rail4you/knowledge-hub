using System;
using System.Collections.Generic;

namespace KnowledgeHub.Practicums.WasmMirrors;

/// <summary>
/// 学生页面用的精简映射表：sourceUrl → publicUrl/status。
/// </summary>
public class WasmMirrorMappingDto
{
    /// <summary>
    /// 规范化后的 sourceUrl（去除末尾斜杠、查询串），前端用作 lookup key。
    /// </summary>
    public string SourceUrl { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// 本地镜像的 publicUrl，形如 <c>https://localhost/wasm/{slug}/index.html</c>。
    /// </summary>
    public string PublicUrl { get; set; } = string.Empty;

    /// <summary>
    /// <c>ready</c> 表示已就绪；其他值（<c>missing</c> / <c>syncing</c> / <c>invalid</c>）时前端应回退到原始 sourceUrl。
    /// </summary>
    public string Status { get; set; } = "missing";
}

/// <summary>
/// 管理/诊断用的完整信息：包含文件清单、字节数、最后修改时间。
/// </summary>
public class WasmMirrorInfoDto : WasmMirrorMappingDto
{
    public string EntryPath { get; set; } = "index.html";

    public int FileCount { get; set; }

    public long TotalBytes { get; set; }

    public DateTime? LastModifiedTime { get; set; }

    public DateTime? MirroredAt { get; set; }

    public string? BuildSha { get; set; }

    public bool CoopCoepRequired { get; set; }

    public List<string> Files { get; set; } = new();

    /// <summary>
    /// 展示用标题。优先使用 mirror.json 中的 title；缺失时由后端用 slug 美化（首字母大写、把 - 改为空格）。
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 封面图 URL（可空）。建议使用 https URL；http 页面里的 https iframe 加载 http 图可能被 Mixed Content 拦截。
    /// </summary>
    public string? Cover { get; set; }

    /// <summary>
    /// 描述（可空）。
    /// </summary>
    public string? Description { get; set; }
}
