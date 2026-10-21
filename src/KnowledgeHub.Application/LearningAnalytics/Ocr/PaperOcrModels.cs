using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.LearningAnalytics.Ocr;

/// <summary>一张试卷图片的 OCR 请求。</summary>
public class PaperOcrRequest
{
    public byte[] Content { get; set; } = Array.Empty<byte>();

    /// <summary>图片公网 URL（与 <see cref="Content"/> 二选一；提供时优先用 URL）</summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// PDF 公网 URL：走 qwen3.5-ocr 的 Responses API（`input_file.file_url`）直接解析整份 PDF，
    /// 无需前端拆页。与 <see cref="Content"/> / <see cref="ImageUrl"/> 互斥。
    /// </summary>
    public string? PdfUrl { get; set; }
    public string ContentType { get; set; } = "image/jpeg";
    public string FileName { get; set; } = string.Empty;
    /// <summary>课程名（仅用于提示模型学科语境，不含题库信息）</summary>
    public string CourseName { get; set; } = string.Empty;
}

/// <summary>OCR 识别出的单题得分。</summary>
public record PaperOcrQuestion(
    string QuestionNo,
    double? Score,
    double? FullScore,
    string? RawText);

/// <summary>OCR 识别结果（结构由代码计算，模型只负责读图）。</summary>
public record PaperOcrResult(
    string? StudentNumber,
    string? StudentName,
    double? TotalScore,
    List<PaperOcrQuestion> Questions,
    double Confidence,
    string RawJson,
    string Provider,
    string Model);

/// <summary>
/// 试卷 OCR 识别器。
/// 实现：<see cref="QwenPaperOcrRecognizer"/>（真实，Qwen-VL-OCR）。
/// 接入其他 OCR 供应商只需新增实现并在 <see cref="PaperOcrRecognizerResolver"/> 中注册。
/// </summary>
public interface IPaperOcrRecognizer
{
    /// <summary>识别器标识（如 qwen）</summary>
    string Provider { get; }

    /// <summary>当前配置是否可用（未配置 Key 时 Qwen 返回 false）</summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    Task<PaperOcrResult> RecognizeAsync(PaperOcrRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// 选择识别器：目前只支持真实 OCR（Qwen-VL-OCR）。
/// 未配置 Qwen API Key 时直接抛错，不再降级为生成假成绩的 Mock 识别器。
/// </summary>
public class PaperOcrRecognizerResolver : ITransientDependency
{
    private readonly QwenPaperOcrRecognizer _qwen;

    public PaperOcrRecognizerResolver(QwenPaperOcrRecognizer qwen)
    {
        _qwen = qwen;
    }

    public async Task<IPaperOcrRecognizer> ResolveAsync(bool preferQwen, CancellationToken cancellationToken = default)
    {
        if (await _qwen.IsAvailableAsync(cancellationToken))
        {
            return _qwen;
        }

        throw new UserFriendlyException("未配置 Qwen API Key，无法执行试卷 OCR 识别（请在「AI 使用管理」中配置）");
    }
}
