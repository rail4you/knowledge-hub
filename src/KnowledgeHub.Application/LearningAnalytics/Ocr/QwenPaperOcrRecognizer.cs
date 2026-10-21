using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Application.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.LearningAnalytics.Ocr;

/// <summary>
/// 基于 Qwen-VL-OCR 的试卷识别（阿里云百炼 OpenAI 兼容接口）。
///
/// 选用自定义 Prompt 直接索要结构化 JSON，而不是 DashScope 专有 `ocr_options`：
/// 1) 与项目现有 `Qwen:BaseUrl` / 动态 Key / 全局限流完全复用；
/// 2) 不依赖地域与业务空间（maas.aliyuncs.com 新地址）；
/// 3) 输出的题目结构直接可入库，避免二次解析坐标。
/// </summary>
public class QwenPaperOcrRecognizer : IPaperOcrRecognizer, ITransientDependency
{
    public const string DefaultModel = "qwen-vl-ocr";

    /// <summary>PDF 直传使用的模型（仅 qwen3.5-ocr 及以后支持 Responses API 传 PDF）</summary>
    public const string DefaultPdfModel = "qwen3.5-ocr";

    /// <summary>
    /// PDF 场景不使用「严格 JSON」提示词：模型容易把题干塞进题号并丢掉分数。
    /// 改为要求**逐行忠实转写**（含学号/姓名/每题得分/满分/总分），结构化解析交给本地 <c>ParseTranscript</c>。
    /// </summary>
    private const string PdfTranscriptPrompt =
        "请逐行完整转写这份试卷 PDF 中的全部文字：学号、姓名、每一题的题号与题干、老师手写的得分（如「得分：6分」）、" +
        "题干末尾印刷的满分（如「（5分）」）、以及总分。要求：不要总结、不要省略题目、不要改写或推算数字，" +
        "看不清的字用 ? 代替；保持原始行顺序；判断题的作答请写成中文「正确」或「错误」（不要写 true/false/T/F）。";

    /// <summary>
    /// 图片场景的「逐行转写」提示词（结构化 JSON 解析失败时的兜底重试）。
    /// 模型偶尔会输出**非法 JSON**（例如把带引号的选项写进 rawText），此时结构化解析会整份失败、
    /// 所有题目得分退化成 0；改用逐行转写可以让本地按「（N分）/ 得分：X分」规则稳定解析。
    /// </summary>
    private const string ImageTranscriptPrompt =
        "请逐行完整转写这张试卷图片上的全部文字：学号、姓名、班级、每一道题的题号与题干、老师批改的得分（如「得分：6分」）、" +
        "题干末尾印刷的满分（如「（5分）」）、以及总分。要求：只转写、不要总结、不要省略题目、不要改写或推算数字，" +
        "看不清的字用 ? 代替；保持原始行顺序；每道题单独一行，该行必须同时包含题号、满分（如「（5分）」）和「得分：X分」；" +
        "判断题的作答请写成中文「正确」或「错误」（不要写 true/false/T/F）。";

    /// <summary>卷头 / 落款行前缀：这些行不是题目，避免被误判为一道题</summary>
    private static readonly Regex HeaderOrFooterLineRegex = new(
        @"^(总分|小计|姓名|学号|考号|班级|课程|科目|考试日期|考试时间|考试|阅卷人|阅卷教师|阅卷|评卷人|院系|专业|座位号|考生|考场)",
        RegexOptions.Compiled);

    /// <summary>JSON 形态的行（"rawText": ... / "score": ...）：逐行转写解析时应跳过</summary>
    private static readonly Regex JsonLikeLineRegex = new(
        "\"[A-Za-z_]+\"\\s*:",
        RegexOptions.Compiled);

    private readonly IConfiguration _configuration;
    private readonly IQwenCredentialProvider _credentialProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<QwenPaperOcrRecognizer> _logger;

    public QwenPaperOcrRecognizer(
        IConfiguration configuration,
        IQwenCredentialProvider credentialProvider,
        IHttpClientFactory httpClientFactory,
        ILogger<QwenPaperOcrRecognizer> logger)
    {
        _configuration = configuration;
        _credentialProvider = credentialProvider;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string Provider => "qwen";

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var key = await _credentialProvider.GetApiKeyAsync();
            return !string.IsNullOrWhiteSpace(key)
                   && !key.Contains("YOUR_QWEN_API_KEY", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public async Task<PaperOcrResult> RecognizeAsync(PaperOcrRequest request, CancellationToken cancellationToken = default)
    {
        // PDF 直传：qwen3.5-ocr 通过 Responses API 的 input_file.file_url 解析整份 PDF（最多 50 页）
        if (!string.IsNullOrWhiteSpace(request.PdfUrl))
        {
            return await RecognizePdfAsync(request, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(request.ImageUrl) &&
            (request.Content == null || request.Content.Length == 0))
        {
            throw new UserFriendlyException("试卷图片内容为空");
        }

        var apiKey = await _credentialProvider.GetApiKeyAsync();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new UserFriendlyException("未配置 Qwen API Key，无法执行 OCR");
        }

        var baseUrl = _configuration["Qwen:BaseUrl"] ?? "https://dashscope.aliyuncs.com/compatible-mode/v1";
        var model = _configuration["LearningAnalytics:OcrModel"]
                    ?? _configuration["Qwen:OcrModel"]
                    ?? DefaultModel;
        var minPixels = _configuration.GetValue("LearningAnalytics:OcrMinPixels", 3072);
        var maxPixels = _configuration.GetValue("LearningAnalytics:OcrMaxPixels", 8388608);
        var timeoutSeconds = _configuration.GetValue("LearningAnalytics:OcrTimeoutSeconds", 120);

        // 有 OSS URL 时直接让模型按 URL 取图（请求体更小、无需本地落盘）
        var imageUrl = !string.IsNullOrWhiteSpace(request.ImageUrl)
            ? request.ImageUrl!
            : $"data:{NormalizeContentType(request.ContentType)};base64,{Convert.ToBase64String(request.Content)}";

        var content = await SendVisionRequestAsync(
            baseUrl, model, apiKey, imageUrl, BuildPrompt(request.CourseName),
            minPixels, maxPixels, timeoutSeconds, cancellationToken);

        var parsed = ParseResult(content);

        // 兜底重试：模型输出的结构化 JSON 非法 / 没解析出题目或得分时（典型场景：rawText 里出现
        // 未转义的引号，整个 JSON 解析失败 → 所有题目得分退化为 0），改用「逐行转写」提示词再识别一次，
        // 由本地按「（N分）/ 得分：X分」规则解析，对非法 JSON 免疫。
        if (!HasUsableScores(parsed))
        {
            _logger.LogWarning(
                "Qwen OCR 结构化解析未得到可用得分（题目 {Count} 题），改用逐行转写重试",
                parsed.Questions.Count);

            string? transcriptContent = null;
            try
            {
                transcriptContent = await SendVisionRequestAsync(
                    baseUrl, model, apiKey, imageUrl, ImageTranscriptPrompt,
                    minPixels, maxPixels, timeoutSeconds, cancellationToken);
            }
            catch (UserFriendlyException ex)
            {
                _logger.LogWarning(ex, "Qwen OCR 逐行转写重试失败，保留首次解析结果");
            }

            if (!string.IsNullOrWhiteSpace(transcriptContent))
            {
                var transcript = ParseTranscript(transcriptContent);
                if (transcript != null && transcript.Questions.Any(q => q.Score.HasValue))
                {
                    parsed = transcript with
                    {
                        RawJson = content + "\n\n--- transcript retry ---\n" + transcriptContent,
                        Provider = Provider,
                        Model = model,
                    };
                }
            }
        }

        return parsed with
        {
            // RawJson 已在解析/重试时写入（重试时会附上转写原文），这里只补齐来源信息
            RawJson = string.IsNullOrEmpty(parsed.RawJson) ? content : parsed.RawJson,
            Provider = Provider,
            Model = model,
        };
    }

    /// <summary>
    /// 调 Qwen-VL-OCR（chat/completions）识别一张图片，返回模型文本输出。
    /// </summary>
    private async Task<string> SendVisionRequestAsync(
        string baseUrl,
        string model,
        string apiKey,
        string imageUrl,
        string prompt,
        int minPixels,
        int maxPixels,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var body = new
        {
            model,
            temperature = 0,
            max_tokens = 4096,
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "image_url",
                            image_url = new { url = imageUrl },
                            min_pixels = minPixels,
                            max_pixels = maxPixels,
                        },
                        new { type = "text", text = prompt },
                    },
                },
            },
        };

        var json = JsonSerializer.Serialize(body, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        });

        // 复用 Qwen 全局并发限流（与聊天/视频理解共享配额）
        using var lease = await QwenClient.AcquireAsync(_configuration, cancellationToken);

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        HttpResponseMessage response;
        string responseBody;
        try
        {
            response = await client.SendAsync(httpRequest, cts.Token);
            responseBody = await response.Content.ReadAsStringAsync(cts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UserFriendlyException($"OCR 识别超时（>{timeoutSeconds}s），请压缩图片或稍后重试");
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Qwen OCR failed: {Status} - {Body}", (int)response.StatusCode, Truncate(responseBody, 800));
            var hint = responseBody.Contains("model_not_found", StringComparison.OrdinalIgnoreCase)
                ? "（模型不可用，请检查 LearningAnalytics:OcrModel / 账号已开通 qwen-vl-ocr）"
                : string.Empty;
            throw new UserFriendlyException($"OCR 识别失败：HTTP {(int)response.StatusCode}{hint}");
        }

        return ExtractContent(responseBody) ?? throw new UserFriendlyException("OCR 返回内容为空");
    }

    /// <summary>结构化结果是否可用：至少要有一道题识别出得分，否则走逐行转写兜底。</summary>
    private static bool HasUsableScores(PaperOcrResult result) =>
        result.Questions.Any(q => q.Score.HasValue);

    /// <summary>
    /// PDF 直传（Responses API）：
    /// POST {base}/responses
    /// { model: qwen3.5-ocr, input:[{role:user, content:[{type:input_file,file_url},{type:input_text,text}]}],
    ///   ocr_options:{task:document_parsing} }
    /// 输出取 output_text / output[].content[].text / ocr_result（不同版本略有差异）。
    /// </summary>
    private async Task<PaperOcrResult> RecognizePdfAsync(PaperOcrRequest request, CancellationToken cancellationToken)
    {
        var apiKey = await _credentialProvider.GetApiKeyAsync();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new UserFriendlyException("未配置 Qwen API Key，无法执行 OCR");
        }

        var baseUrl = _configuration["Qwen:BaseUrl"] ?? "https://dashscope.aliyuncs.com/compatible-mode/v1";
        var model = _configuration["LearningAnalytics:PdfOcrModel"] ?? DefaultPdfModel;
        var timeoutSeconds = _configuration.GetValue("LearningAnalytics:OcrTimeoutSeconds", 120);

        var body = new
        {
            model,
            input = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "input_file", file_url = request.PdfUrl },
                        new { type = "input_text", text = PdfTranscriptPrompt },
                    },
                },
            },
        };

        var json = JsonSerializer.Serialize(body);

        using var lease = await QwenClient.AcquireAsync(_configuration, cancellationToken);

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(Math.Max(timeoutSeconds, 180));

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/responses");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(timeoutSeconds, 180)));

        var response = await client.SendAsync(httpRequest, cts.Token);
        var responseBody = await response.Content.ReadAsStringAsync(cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Qwen PDF OCR failed: {Status} - {Body}", (int)response.StatusCode, Truncate(responseBody, 800));
            var hint = responseBody.Contains("model_not_found", StringComparison.OrdinalIgnoreCase)
                ? "（PDF 直传需要账号开通 qwen3.5-ocr，可通过 LearningAnalytics:PdfOcrModel 调整）"
                : string.Empty;
            throw new UserFriendlyException($"PDF 识别失败：HTTP {(int)response.StatusCode}{hint}");
        }

        var content = ExtractResponsesText(responseBody);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new UserFriendlyException("PDF 识别返回内容为空");
        }

        // 优先按「纯文本转写」解析（PDF 提示词就是要求转写）；解析不到再退回 JSON 解析
        var parsed = ParseTranscript(content) ?? ParseResult(content);

        return parsed with
        {
            RawJson = content,
            Provider = Provider,
            Model = model,
        };
    }

    /// <summary>兼容 Responses API 的多种输出形态。</summary>
    private static string? ExtractResponsesText(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            // 1) output_text 聚合字段
            if (root.TryGetProperty("output_text", out var outputText) && outputText.ValueKind == JsonValueKind.String)
            {
                return outputText.GetString();
            }

            // 2) output[].content[].text / .ocr_result
            if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var texts = new List<string>();
            foreach (var item in output.EnumerateArray())
            {
                if (!item.TryGetProperty("content", out var contents) || contents.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var part in contents.EnumerateArray())
                {
                    if (part.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                    {
                        texts.Add(text.GetString()!);
                    }
                    else if (part.TryGetProperty("ocr_result", out var ocr) && ocr.ValueKind != JsonValueKind.Null)
                    {
                        texts.Add(ocr.GetRawText());
                    }
                }
            }

            return texts.Any() ? string.Join("\n", texts) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string BuildPrompt(string? courseName)
    {
        var subject = string.IsNullOrWhiteSpace(courseName) ? "未知课程" : courseName;
        return $$"""
            你是职业院校的试卷成绩识别助手。请识别这张试卷图片，并且**只输出 JSON**（不要 markdown 代码块、不要任何解释）。
            课程：{{subject}}

            输出格式：
            {
              "studentNumber": "学号/考号，看不清填空字符串",
              "studentName": "姓名，看不清填空字符串",
              "totalScore": 数字或 null,
              "questions": [
                {"no": "题号，如 1 / 5 / 5(1)", "score": 数字或 null, "fullScore": 数字或 null, "rawText": "原样转写的题行片段"}
              ],
              "confidence": 0 到 1 之间的小数，表示整体识别可信度
            }

            规则：
            1) 只识别试卷上真实出现的题目，不要补全或编造缺失题目；
            2) score / fullScore / totalScore 必须是**纯数字**（如 6、8.5），不要写“6分”“（8分）”这类带文字的内容；
            3) 题干末尾印刷的“（8分）”“本题10分”是**满分** fullScore；老师批改的“得分：6分”是**得分** score；
            4) rawText 必须是**原样抄写**的片段：从题号开始、到“得分：X分”为止，必须同时包含满分与得分，
               例如 "2. 以下属于对称加密算法的是？（5分） 得分：5分"；这样即使 score 写错，程序也能从原文里读出真实分数；
            5) rawText **不要包含选项内容**，也不要把选项整理成 JSON/数组：选项里常有引号与括号，
               写进 JSON 字符串会让你的输出变成非法 JSON，进而导致整份成绩解析失败；
            6) rawText 中不要出现英文双引号 " 或反斜杠，需要强调时用「」代替；换行用 \n 表示；
            7) 如果某题只看到满分、没有看到得分，score 请填 null；反之只看到得分则 fullScore 填 null；
            8) 题号保持卷面原样，不要重排；
            9) 判断题的作答请写成中文「正确」或「错误」（不要写 true/false/T/F/对/错）；
            10) 若图片不是试卷或无法识别，questions 返回空数组且 confidence 小于 0.2。
            """;
    }

    private static string? ExtractContent(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            {
                return null;
            }

            var message = choices[0].GetProperty("message");
            if (!message.TryGetProperty("content", out var contentElement))
            {
                return null;
            }

            // 兼容 content 为字符串或分段数组两种返回形态
            if (contentElement.ValueKind == JsonValueKind.String)
            {
                return contentElement.GetString();
            }

            if (contentElement.ValueKind == JsonValueKind.Array)
            {
                var sb = new StringBuilder();
                foreach (var part in contentElement.EnumerateArray())
                {
                    if (part.ValueKind == JsonValueKind.Object && part.TryGetProperty("text", out var text))
                    {
                        sb.Append(text.GetString());
                    }
                }

                return sb.ToString();
            }

            return contentElement.ToString();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static PaperOcrResult ParseResult(string content)
    {
        var json = ExtractJsonObject(content);
        if (json == null)
        {
            // 模型未按 JSON 输出：尝试把「纯文本转写」解析成结构化结果（PDF 直传常见形态）
            var transcript = ParseTranscript(content);
            if (transcript != null)
            {
                return transcript with { RawJson = content, Provider = "qwen", Model = DefaultModel };
            }

            // 再试一次「容错解析」：输出里已经有 no/score 等字段，只是 JSON 不合法
            var lenient = ParseLenientQuestions(content);
            if (lenient.Questions.Count > 0)
            {
                return lenient with { RawJson = content, Provider = "qwen", Model = DefaultModel };
            }

            return new PaperOcrResult(null, null, null, new List<PaperOcrQuestion>(), 0.2,
                content, "qwen", DefaultModel);
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string? ReadString(string name) =>
                root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;

            double? ReadNumber(string name) =>
                root.TryGetProperty(name, out var value) ? ParseNumeric(value) : null;

            var questions = new List<PaperOcrQuestion>();
            if (root.TryGetProperty("questions", out var array) && array.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in array.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var no = item.TryGetProperty("no", out var noElement)
                        ? noElement.ValueKind == JsonValueKind.String
                            ? noElement.GetString()
                            : noElement.ToString()
                        : null;

                    double? score = item.TryGetProperty("score", out var scoreElement)
                        ? ParseNumeric(scoreElement)
                        : null;

                    double? fullScore = item.TryGetProperty("fullScore", out var fullElement)
                        ? ParseNumeric(fullElement)
                        : null;

                    string? rawText = item.TryGetProperty("rawText", out var rawElement) &&
                                      rawElement.ValueKind == JsonValueKind.String
                        ? rawElement.GetString()
                        : null;

                    questions.Add(BuildQuestion(no, score, fullScore, rawText, questions.Count + 1));
                }
            }

            if (!questions.Any())
            {
                var transcript = ParseTranscript(content);
                if (transcript != null)
                {
                    return transcript with { RawJson = content, Provider = "qwen", Model = DefaultModel };
                }
            }

            var confidence = ReadNumber("confidence") ?? (questions.Any() ? 0.7 : 0.1);
            confidence = Math.Clamp(confidence, 0, 1);

            return new PaperOcrResult(
                ReadString("studentNumber")?.Trim(),
                ReadString("studentName")?.Trim(),
                ReadNumber("totalScore"),
                questions,
                confidence,
                content,
                "qwen",
                DefaultModel);
        }
        catch (JsonException)
        {
            // 模型输出的 JSON 非法（典型：rawText 里出现了未转义的引号，如选项被转成 JSON 数组），
            // 整份结果直接丢弃会导致所有题目得分退化为 0 —— 这里改用容错解析把分数救回来。
            var lenient = ParseLenientQuestions(json);
            if (lenient.Questions.Count > 0)
            {
                return lenient with { RawJson = content, Provider = "qwen", Model = DefaultModel };
            }

            var transcript = ParseTranscript(content);
            if (transcript != null)
            {
                return transcript with { RawJson = content, Provider = "qwen", Model = DefaultModel };
            }

            return new PaperOcrResult(null, null, null, new List<PaperOcrQuestion>(), 0.2,
                content, "qwen", DefaultModel);
        }
    }

    /// <summary>
    /// 单题归一化：把模型的「no / score / fullScore / rawText」整理成可入库的题目。
    ///
    /// 校正策略：
    /// 1) rawText 里的「得分：X分」优先于模型的 score；「（Y分）」优先于模型的 fullScore；
    /// 2) 模型把得分写进了 fullScore（score=null）且与原文里的印刷满分不一致时，认定它其实是得分；
    /// 3) score &gt; fullScore 的明显反常做交换兜底。
    /// </summary>
    private static PaperOcrQuestion BuildQuestion(
        string? no,
        double? score,
        double? fullScore,
        string? rawText,
        int index)
    {
        var modelFullScore = fullScore;

        var scoreFromText = ExtractScoreFromText(rawText);
        var fullFromText = ExtractFullScoreFromText(rawText);
        if (scoreFromText.HasValue)
        {
            score = scoreFromText;
        }

        if (fullFromText.HasValue)
        {
            fullScore = fullFromText;
        }

        // 模型把「得分」写进了 fullScore（score 为 null）：用原文里的印刷满分做对照，纠正回来
        if (!score.HasValue &&
            fullFromText.HasValue &&
            modelFullScore.HasValue &&
            Math.Abs(modelFullScore.Value - fullFromText.Value) > 0.001)
        {
            score = modelFullScore;
        }

        if (score.HasValue && fullScore.HasValue && score.Value > fullScore.Value + 0.001)
        {
            (score, fullScore) = (fullScore, score);
        }

        var normalizedNo = NormalizeQuestionNo(no, index);

        // 模型可能把整行题干放进 no，且不给分数：从题面里补抽「得分：X分」「（Y分）」
        var probe = $"{no} {rawText}";
        score ??= ExtractScoreFromText(probe);
        fullScore ??= ExtractFullScoreFromText(probe);
        if (string.IsNullOrWhiteSpace(rawText))
        {
            rawText = string.IsNullOrWhiteSpace(no) ? null : (no!.Length > 60 ? no[..60] : no);
        }

        return new PaperOcrQuestion(normalizedNo, score, fullScore, rawText);
    }

    /// <summary>
    /// 容错解析：模型的 JSON 非法时（字段还在，只是引号没转义），按「no / score / fullScore / rawText」
    /// 逐块扫描文本，把能读到的题目与分数救回来，避免整份成绩退化为 0。
    /// </summary>
    private static PaperOcrResult ParseLenientQuestions(string text)
    {
        var questions = new List<PaperOcrQuestion>();

        // 以 "no": 为分隔切块（每块含一道题的 no/score/fullScore/rawText）
        var blocks = Regex.Split(text, "(?=\"no\"\\s*:)");

        for (var i = 1; i < blocks.Length; i++)
        {
            var block = blocks[i];

            var no = Regex.Match(block, "\"no\"\\s*:\\s*\"?([^\"\\n,}]{1,32})").Groups[1].Value;
            var score = ParseLooseNumber(block, "score");
            var fullScore = ParseLooseNumber(block, "fullScore");
            var rawText = Regex.Match(block, "\"rawText\"\\s*:\\s*\"(.*?)(?=\"|$)",
                RegexOptions.Singleline).Groups[1].Value;

            if (string.IsNullOrWhiteSpace(no) && string.IsNullOrWhiteSpace(rawText))
            {
                continue;
            }

            questions.Add(BuildQuestion(
                string.IsNullOrWhiteSpace(no) ? null : no,
                score,
                fullScore,
                string.IsNullOrWhiteSpace(rawText) ? null : rawText,
                questions.Count + 1));
        }

        if (!questions.Any())
        {
            return new PaperOcrResult(null, null, null, new List<PaperOcrQuestion>(), 0.2,
                text, "qwen", DefaultModel);
        }

        string? ReadLooseString(string key)
        {
            var m = Regex.Match(text, $"\"{key}\"\\s*:\\s*\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        var totalMatch = Regex.Match(text, "\"totalScore\"\\s*:\\s*\"?([0-9]+(?:\\.[0-9]+)?)");
        double? total = totalMatch.Success &&
                        double.TryParse(totalMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var t)
            ? t
            : questions.Where(q => q.Score.HasValue).Sum(q => q.Score!.Value);

        var confidenceMatch = Regex.Match(text, "\"confidence\"\\s*:\\s*([0-9]+(?:\\.[0-9]+)?)");
        var confidence = confidenceMatch.Success &&
                         double.TryParse(confidenceMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var c)
            ? Math.Clamp(c, 0, 1)
            : 0.5;

        return new PaperOcrResult(
            ReadLooseString("studentNumber"),
            ReadLooseString("studentName"),
            total,
            questions,
            confidence,
            text,
            "qwen",
            DefaultModel);
    }

    /// <summary>容错读一个数值字段：兼容 5 / "5" / "5分" / null。</summary>
    private static double? ParseLooseNumber(string block, string key)
    {
        var match = Regex.Match(block,
            $"\"{key}\"\\s*:\\s*(?:\"(?<quoted>[0-9]+(?:\\.[0-9]+)?)\\s*分?\"|(?<plain>[0-9]+(?:\\.[0-9]+)?))");

        if (!match.Success)
        {
            return null;
        }

        var value = match.Groups["quoted"].Success ? match.Groups["quoted"].Value : match.Groups["plain"].Value;
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    /// <summary>
    /// 解析「纯文本转写」形态的 OCR 结果（PDF 直传 / 模型未按 JSON 输出时）：
    /// 从「学号：xxx」「姓名：xxx」「N. 题干 得分：X分（Y分）」「总分：Z」中抽取结构化信息。
    /// </summary>
    internal static PaperOcrResult? ParseTranscript(string text)    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        // PDF 直传时模型可能返回 HTML/LaTeX 包裹的转写：先去掉标签与实体，再按行解析
        var plain = System.Net.WebUtility.HtmlDecode(Regex.Replace(text, "<[^>]+>", "\n"))
            .Replace("：", ":").Replace("（", "(").Replace("）", ")");

        var numberMatch = Regex.Match(plain, @"学号\s*:\s*([0-9A-Za-z_-]{3,32})");
        var nameMatch = Regex.Match(plain, @"姓名\s*:\s*([\u4e00-\u9fa5]{2,6})");
        var totalMatch = Regex.Match(plain, @"总分\s*:\s*(\d+(?:\.\d+)?)");

        var questions = new List<PaperOcrQuestion>();
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in plain.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length < 4 || !line.Contains('分'))
            {
                continue;
            }

            // JSON 形态的行（如 "rawText": "1. 某题（5分）..."）不是转写文本，跳过，
            // 否则会把 JSON 字段值当成题目，产生重复/错误题目。
            if (JsonLikeLineRegex.IsMatch(line))
            {
                continue;
            }

            // 单独成行的「得分：X分」（模型把得分另起一行）：补到上一道还没有得分的题上
            var scoreOnly = Regex.Match(line, @"^得分\s*:\s*(\d+(?:\.\d+)?)\s*分?");
            if (scoreOnly.Success && questions.Count > 0)
            {
                var last = questions[^1];
                if (!last.Score.HasValue &&
                    double.TryParse(scoreOnly.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var solo))
                {
                    questions[^1] = last with { Score = solo };
                }

                continue;
            }

            // 卷头/落款行（学号、姓名、班级、总分、阅卷人…）不是题目，跳过，
            // 否则「总分：27分」会被当成一道题，污染题目与得分对应关系。
            if (HeaderOrFooterLineRegex.IsMatch(line))
            {
                continue;
            }

            var (score, fullScore) = ExtractScoresFromLine(line);
            if (score == null)
            {
                continue;
            }

            var index = questions.Count + 1;
            var no = NormalizeQuestionNo(line, index);

            // 同一份 PDF 多页时题号可能重复：加后缀区分，便于人工校对
            if (seen.TryGetValue(no, out var times))
            {
                seen[no] = times + 1;
                no = $"{no}-{times + 1}";
            }
            else
            {
                seen[no] = 1;
            }

            questions.Add(new PaperOcrQuestion(
                no,
                score,
                fullScore,
                line.Length > 60 ? line[..60] : line));
        }

        if (!questions.Any())
        {
            return null;
        }

        double? total = totalMatch.Success &&
                        double.TryParse(totalMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var t)
            ? t
            : questions.Where(q => q.Score.HasValue).Sum(q => q.Score!.Value);

        return new PaperOcrResult(
            numberMatch.Success ? numberMatch.Groups[1].Value : null,
            nameMatch.Success ? nameMatch.Groups[1].Value : null,
            total,
            questions,
            0.6, // PDF 直传的转写与得分对应关系可能错位，按低置信度处理，强制人工校对
            text,
            "qwen",
            DefaultPdfModel);
    }

    /// <summary>
    /// 从一行文本里抽取「得分 / 满分」：
    /// - 得分 = 第一个**不在括号内**的「N分」（如「得分：6分」「?4分 )」）；
    /// - 满分 = 最后一个**在括号内**的「N分」（如「(5分)」），或「本题/满分 N分」；
    /// 若得分 &gt; 满分则交换（模型偶发写反）。
    /// </summary>
    internal static (double? Score, double? FullScore) ExtractScoresFromLine(string line)
    {
        // 全角括号（（5分））统一成半角，避免「印刷满分」被当成「手写得分」
        var normalized = (line ?? string.Empty)
            .Replace("（", "(")
            .Replace("）", ")")
            .Replace("：", ":");

        var candidates = new List<(double Value, bool InParen)>();
        foreach (Match m in Regex.Matches(normalized, @"(\d+(?:\.\d+)?)\s*分"))
        {
            if (!double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                continue;
            }

            var before = normalized[..m.Index].TrimEnd();
            var inParen = before.EndsWith("(") || before.EndsWith("本题") || before.EndsWith("满分");
            candidates.Add((value, inParen));
        }

        if (!candidates.Any())
        {
            return (null, null);
        }

        double? score = candidates.FirstOrDefault(c => !c.InParen).Value;
        var scoreFound = candidates.Any(c => !c.InParen);
        if (!scoreFound)
        {
            // 只有括号内的数字：视为「只有满分、没读到得分」
            var parenOnly = candidates.Last().Value;
            return (null, parenOnly);
        }

        double? full = candidates.LastOrDefault(c => c.InParen).Value;
        full ??= ExtractFullScoreFromText(normalized);

        // 兜底：模型写反时交换
        if (score.HasValue && full.HasValue && score.Value > full.Value + 0.001)
        {
            (score, full) = (full, score);
        }

        return (score, full);
    }

    /// <summary>宽容解析数字：兼容 number、以及「6」「6分」「得分：6分」「（8分）」等字符串。</summary>
    internal static double? ParseNumeric(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.GetDouble();
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return ExtractFirstNumber(element.GetString());
    }

    internal static double? ExtractFirstNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var normalized = text
            .Replace("（", "(").Replace("）", ")")
            .Replace("：", ":")
            .Replace("．", ".").Replace("。", ".");

        var match = Regex.Match(normalized, @"-?\d+(?:\.\d+)?");
        if (!match.Success)
        {
            return null;
        }

        return double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    /// <summary>
    /// 题号归一化：模型有时把整行题干写进 no。
    /// 取开头的数字编号（支持 5(1) / 5.1 / 1、），取不到就用序号；最长 16 字符。
    /// </summary>
    internal static string NormalizeQuestionNo(string? rawNo, int index)
    {
        var text = (rawNo ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return index.ToString(CultureInfo.InvariantCulture);
        }

        // 只取开头 1~3 位数字（避免把「2.192.168.1.0/24」解析成 2.19）
        var leading = Regex.Match(text, @"^\s*(\d{1,3})");
        if (!leading.Success)
        {
            return index.ToString(CultureInfo.InvariantCulture);
        }

        var candidate = leading.Groups[1].Value;

        // 仅保留显式括号子题号（5(1)），避免把「2.192.168.1.0」误判成 2.19
        var sub = Regex.Match(text, @"^\s*\d{1,3}\s*[（(]\s*(\d{1,2})\s*[)）]");
        if (sub.Success)
        {
            candidate = $"{candidate}.{sub.Groups[1].Value}";
        }

        return candidate;
    }

    /// <summary>从题干片段里抽取满分标注，如「（8分）」「本题10分」「满分12分」。</summary>
    internal static double? ExtractFullScoreFromText(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return null;
        }

        var normalized = rawText.Replace("：", ":").Replace("（", "(").Replace("）", ")")
            .Replace("．", ".").Replace("。", ".");

        var match = Regex.Match(normalized,
            @"(?:本题|满分|总分)\s*[:：]?\s*(\d+(?:\.\d+)?)\s*分|[(]\s*(\d+(?:\.\d+)?)\s*分\s*[)]");

        if (!match.Success)
        {
            return null;
        }

        var value = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    /// <summary>从题干片段里抽取手写得分，如「得分：6分」「得分 6」「6分」。</summary>
    internal static double? ExtractScoreFromText(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return null;
        }

        var normalized = rawText.Replace("：", ":").Replace("（", "(").Replace("）", ")");
        var match = Regex.Match(normalized, @"得分\s*[:：]?\s*(\d+(?:\.\d+)?)\s*分?");
        if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        return null;
    }

    private static string? ExtractJsonObject(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = raw.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline >= 0)
            {
                text = text[(firstNewline + 1)..];
            }

            var fence = text.LastIndexOf("```", StringComparison.Ordinal);
            if (fence >= 0)
            {
                text = text[..fence];
            }
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : null;
    }

    private static string NormalizeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType) || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return "image/jpeg";
        }

        return contentType.Split(';')[0].Trim();
    }

    private static string Truncate(string? text, int max) =>
        string.IsNullOrEmpty(text) ? string.Empty : text.Length <= max ? text : text[..max];
}
