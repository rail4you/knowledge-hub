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
        "看不清的字用 ? 代替；保持原始行顺序。";

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
                        new { type = "text", text = BuildPrompt(request.CourseName) },
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

        var content = ExtractContent(responseBody) ?? throw new UserFriendlyException("OCR 返回内容为空");
        var parsed = ParseResult(content);

        return parsed with
        {
            RawJson = content,
            Provider = Provider,
            Model = model,
        };
    }

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
                {"no": "题号，如 1 / 5 / 5(1)", "score": 数字或 null, "fullScore": 数字或 null, "rawText": "题干前20字用于匹配，看不清留空"}
              ],
              "confidence": 0 到 1 之间的小数，表示整体识别可信度
            }

            规则：
            1) 只识别试卷上真实出现的题目，不要补全或编造缺失题目；
            2) score / fullScore / totalScore 必须是**数字**（如 6、8.5），不要写“（8分）”这类带文字的内容；
            3) 题干末尾印刷的“（8分）”“本题10分”是**满分** fullScore；老师手写/批改的分数是**得分** score；
            4) 如果某题只看到满分、没有看到得分，score 请填 null；反之只看到得分则 fullScore 填 null；
            5) 题号保持卷面原样，不要重排；rawText 保留题干前 20 字，可附上你读到的手写得分文本；
            6) 若图片不是试卷或无法识别，questions 返回空数组且 confidence 小于 0.2。
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

                    // 模型偶尔会把「满分 / 得分」写反（甚至写进 rawText 里）：
                    // 优先用 rawText 里的「得分：X分」「（Y分）」校正；再用 score > fullScore 的明显反常做兜底交换。
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

                    if (score.HasValue && fullScore.HasValue && score.Value > fullScore.Value + 0.001)
                    {
                        (score, fullScore) = (fullScore, score);
                    }

                    var index = questions.Count + 1;
                    var normalizedNo = NormalizeQuestionNo(no, index);

                    // 模型可能把整行题干放进 no，且不给分数：从题面里补抽「得分：X分」「（Y分）」
                    var probe = $"{no} {rawText}";
                    score ??= ExtractScoreFromText(probe);
                    fullScore ??= ExtractFullScoreFromText(probe);
                    if (string.IsNullOrWhiteSpace(rawText))
                    {
                        rawText = string.IsNullOrWhiteSpace(no) ? null : (no!.Length > 60 ? no[..60] : no);
                    }

                    questions.Add(new PaperOcrQuestion(normalizedNo, score, fullScore, rawText));
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
            return new PaperOcrResult(null, null, null, new List<PaperOcrQuestion>(), 0.2,
                content, "qwen", DefaultModel);
        }
    }

    /// <summary>
    /// 解析「纯文本转写」形态的 OCR 结果（PDF 直传 / 模型未按 JSON 输出时）：
    /// 从「学号：xxx」「姓名：xxx」「N. 题干 得分：X分（Y分）」「总分：Z」中抽取结构化信息。
    /// </summary>
    internal static PaperOcrResult? ParseTranscript(string text)
    {
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
        var candidates = new List<(double Value, bool InParen)>();
        foreach (Match m in Regex.Matches(line, @"(\d+(?:\.\d+)?)\s*分"))
        {
            if (!double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                continue;
            }

            var before = line[..m.Index].TrimEnd();
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
        full ??= ExtractFullScoreFromText(line);

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
