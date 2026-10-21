using System;
using System.Collections.Generic;
using System.Linq;
using KnowledgeHub.Exams;
using KnowledgeHub.Exams.Enums;
using KnowledgeHub.LearningAnalytics.Ocr;
using Shouldly;
using Xunit;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>
/// 纸质试卷 OCR 解析回归测试。
///
/// 依据 scripts/paper-exam-sample 生成的《网络安全技术》试卷样张，
/// 校验两种 OCR 输出形态都能解析出正确的题号 / 得分 / 满分：
/// 1) 图片 OCR（qwen-vl-ocr）：模型按 JSON 输出；
/// 2) PDF 直传（qwen3.5-ocr）：模型按逐行转写输出。
/// </summary>
public class PaperOcrParsingTests
{
    /// <summary>样张卷面：6 道题，学生得分 5 / 5 / 5 / 7 / 0 / 5，满分 5 / 5 / 5 / 10 / 5 / 5。</summary>
    private const string Transcript = """
        网络安全技术 期末考试试卷
        学号：demo_student 姓名：张晨 班级：计算机应用技术 2301 班 考试日期：2026-10-20 课程：网络安全技术
        一、判断题
        1. HTTPS 使用 TLS 协议保证传输过程中的机密性与完整性。（5分） 得分：5分
        作答：正确
        二、单选题
        2. 以下属于对称加密算法的是？（5分） 得分：5分
        A. RSA B. AES C. ECDSA D. SHA-256
        作答：B
        三、填空题
        3. 数字签名主要保证数据的完整性和 ______ 性。（5分） 得分：5分
        作答：不可否认
        四、简答题
        4. 简述对称加密与非对称加密的区别及适用场景。（10分） 得分：7分
        作答：对称加密加解密用同一把密钥，速度快，适合大批量数据加密；非对称加密用公钥私钥对，速度慢，适合密钥交换和数字签名。
        五、单选题
        5. 防火墙的默认策略应遵循哪一原则？（5分） 得分：0分
        A. 全部允许 B. 最小权限 C. 随机放行 D. 按 IP 长度放行
        作答：A
        六、单选题
        6. 用于检测网络入侵行为的系统缩写是？（5分） 得分：5分
        A. IDS B. DNS C. DHCP D. NAT
        作答：A
        阅卷人签字：王老师 总分：27分 / 35 分
        """;

    [Fact]
    public void 图片OCR_JSON输出_应解析出题号_得分_满分()
    {
        const string json = """
            {
              "studentNumber": "demo_student",
              "studentName": "张晨",
              "totalScore": 27,
              "questions": [
                {"no": "1", "score": 5, "fullScore": 5, "rawText": "HTTPS 使用 TLS 协议保证传输过程"},
                {"no": "2", "score": 5, "fullScore": 5, "rawText": "以下属于对称加密算法的是"},
                {"no": "3", "score": 5, "fullScore": 5, "rawText": "数字签名主要保证数据的完整性和"},
                {"no": "4", "score": 7, "fullScore": 10, "rawText": "简述对称加密与非对称加密的区别"},
                {"no": "5", "score": 0, "fullScore": 5, "rawText": "防火墙的默认策略应遵循哪一原则"},
                {"no": "6", "score": 5, "fullScore": 5, "rawText": "用于检测网络入侵行为的系统缩写是"}
              ],
              "confidence": 0.92
            }
            """;

        var result = QwenPaperOcrRecognizer.ParseResult(json);

        result.Questions.Count.ShouldBe(6);
        result.StudentName.ShouldBe("张晨");
        result.StudentNumber.ShouldBe("demo_student");
        result.TotalScore.ShouldBe(27);
        result.Confidence.ShouldBeGreaterThan(0.8);

        // 题号顺序 = 题库顺序（章节 sortOrder + 建题时间），保证顺序匹配可用
        result.Questions.Select(q => q.QuestionNo).ShouldBe(new[] { "1", "2", "3", "4", "5", "6" });
        result.Questions.Select(q => q.Score!.Value).ShouldBe(new[] { 5d, 5d, 5d, 7d, 0d, 5d });
        result.Questions.Select(q => q.FullScore!.Value).ShouldBe(new[] { 5d, 5d, 5d, 10d, 5d, 5d });
    }

    [Fact]
    public void PDF转写输出_应解析出同样的题目与得分()
    {
        var result = QwenPaperOcrRecognizer.ParseTranscript(Transcript);

        result.ShouldNotBeNull();
        result!.StudentName.ShouldBe("张晨");
        result.StudentNumber.ShouldBe("demo_student");

        result.Questions.Count.ShouldBe(6);
        result.Questions.Select(q => q.QuestionNo).ShouldBe(new[] { "1", "2", "3", "4", "5", "6" });
        result.Questions.Select(q => q.Score!.Value).ShouldBe(new[] { 5d, 5d, 5d, 7d, 0d, 5d });
        result.Questions.Select(q => q.FullScore!.Value).ShouldBe(new[] { 5d, 5d, 5d, 10d, 5d, 5d });
    }

    [Fact]
    public void PDF转写输出_卷头与落款行不应被当成题目()
    {
        var result = QwenPaperOcrRecognizer.ParseTranscript(Transcript);

        result.ShouldNotBeNull();

        // 「总分：27分 / 35 分」「阅卷人签字」等落款行不产生题目
        result!.Questions.Any(q => q.RawText != null && q.RawText.Contains("总分")).ShouldBeFalse();
        result.Questions.Any(q => q.RawText != null && q.RawText.Contains("阅卷")).ShouldBeFalse();

        // 总分取题目得分之和（卷面落款行已被跳过）
        result.TotalScore.ShouldBe(27);
    }

    [Fact]
    public void 题号与得分成对提取_手写得分优先于印刷满分()
    {
        // 同一行里既有印刷满分（括号内）又有手写得分（括号外）
        var (score, full) = QwenPaperOcrRecognizer.ExtractScoresFromLine("1. 某题（10分） 得分：7分");
        score.ShouldBe(7);
        full.ShouldBe(10);

        // 只有印刷满分时，得分为空（交由人工校对）
        var (scoreOnly, fullOnly) = QwenPaperOcrRecognizer.ExtractScoresFromLine("2. 另一题（5分）");
        scoreOnly.ShouldBeNull();
        fullOnly.ShouldBe(5);
    }

    [Fact]
    public void 题号归一化_应去掉题干前缀并保留子题号()
    {
        QwenPaperOcrRecognizer.NormalizeQuestionNo("5(1)", 1).ShouldBe("5.1");
        QwenPaperOcrRecognizer.NormalizeQuestionNo("12. 下列说法正确的是", 1).ShouldBe("12");
        QwenPaperOcrRecognizer.NormalizeQuestionNo("2.192.168.1.0/24 网段", 1).ShouldBe("2");
    }

    /// <summary>
    /// 回归：模型把选项（含双引号的 JSON）写进 rawText，导致输出的 JSON **不合法**，
    /// 整份解析失败 → 题目得分全部退化为 0、置信度 20%。
    /// 这里保证即使 JSON 非法，也能把每题得分救回来。
    /// </summary>
    [Fact]
    public void 模型输出非法JSON时_应容错解析出每题得分()
    {
        // 真实场景（qwen-vl-ocr）的输出：rawText 里含未转义的 " 导致 JSON 非法
        const string broken = """
            ```json
            {
                "studentNumber": "demo_student",
                "studentName": "张晨学生",
                "totalScore": null,
                "questions": [
                    {
                        "no": "1.",
                        "score": null,
                        "fullScore": "1分",
                        "rawText": "1. 以下属于对称加密算法的是？（5分）\nA. [{"key":"A","content":"RSA"}]\n作答：B"
                    },
                    {
                        "no": "2.",
                        "score": "5分",
                        "fullScore": "5分",
                        "rawText": "2. 用于检测网络入侵行为的系统缩写是？（5分）\nA. [{"key":"A","content":"IDS"}]\n作答：A"
                    },
                    {
                        "no": "3.",
                        "score": "2分",
                        "fullScore": "10分",
                        "rawText": "3. 简述对称加密与非对称加密的区别及适用场景。（10分）\n作答：对称加密加解密使用同一密钥"
                    },
                    {
                        "no": "4.",
                        "score": "2分",
                        "fullScore": "5分",
                        "rawText": "4. HTTPS 使用 TLS 协议保证传输过程中的机密性与完整性。（5分）\n作答：true"
                    },
                    {
                        "no": "5.",
                        "score": "0分",
                        "fullScore": "5分",
                        "rawText": "5. 防火墙的默认策略应遵循哪一原则？（5分）\nA. [{"key":"A","content":"全部允许"}]\n作答：B"
                    },
                    {
                        "no": "6.",
                        "score": "2分",
                        "fullScore": "5分",
                        "rawText": "6. 数字签名主要保证数据的完整性和______性。（5分）\n作答：不可否认"
                    }
                ],
                "confidence": 0.97
            }
            ```
            """;

        var result = QwenPaperOcrRecognizer.ParseResult(broken);

        result.Questions.Count.ShouldBe(6);
        result.StudentNumber.ShouldBe("demo_student");
        result.StudentName.ShouldBe("张晨学生");

        // 每题得分都要被救回来（总分 12）
        result.Questions.Select(q => q.Score!.Value).ShouldBe(new[] { 1d, 5d, 2d, 2d, 0d, 2d });
        result.Questions.Select(q => q.FullScore!.Value).ShouldBe(new[] { 5d, 5d, 10d, 5d, 5d, 5d });
        result.TotalScore.ShouldBe(12);
    }

    /// <summary>回归：模型把得分写进 fullScore、score 留空时，应从原文的印刷满分对照纠回来。</summary>
    [Fact]
    public void 模型把得分写进fullScore时_应纠正为得分()
    {
        const string json = """
            {
              "studentNumber": "demo_student",
              "studentName": "张晨",
              "totalScore": 12,
              "questions": [
                {"no": "1", "score": null, "fullScore": 1, "rawText": "1. 以下属于对称加密算法的是？（5分） 得分：1分"},
                {"no": "2", "score": 5, "fullScore": 5, "rawText": "2. 用于检测网络入侵行为的系统缩写是？（5分） 得分：5分"}
              ],
              "confidence": 0.9
            }
            """;

        var result = QwenPaperOcrRecognizer.ParseResult(json);

        result.Questions[0].Score.ShouldBe(1);
        result.Questions[0].FullScore.ShouldBe(5);
        result.Questions[1].Score.ShouldBe(5);
        result.Questions[1].FullScore.ShouldBe(5);
    }

    /// <summary>回归：逐行转写解析时，JSON 形态的行不能被当成题目。</summary>
    [Fact]
    public void 逐行转写解析_应跳过JSON形态的行()
    {
        const string mixed = """
            {
                "studentNumber": "demo_student",
                "questions": [
                    {"no": "1.", "score": null, "fullScore": "1分", "rawText": "1. 某题（5分）"}
                ]
            }
            1. 某题（5分） 得分：1分
            """;

        var result = QwenPaperOcrRecognizer.ParseTranscript(mixed);

        result.ShouldNotBeNull();
        result!.Questions.Count.ShouldBe(1);
        result.Questions[0].Score.ShouldBe(1);
        result.Questions[0].FullScore.ShouldBe(5);
    }

    /// <summary>回归：选项以 [{key,content}] 形式存储时，卷面不能印出整段 JSON。</summary>
    [Fact]
    public void 试卷选项解析_应支持key_content对象数组()
    {
        var options = PaperTemplateGenerator.ParseOptions(
            "[{\"key\":\"A\",\"content\":\"RSA\"},{\"key\":\"B\",\"content\":\"AES\"}]");

        options.ShouldBe(new[] { "RSA", "AES" });

        // 纯字符串数组也要正常
        PaperTemplateGenerator.ParseOptions("[\"A. 曲线\",\"B. 直线\"]")
            .ShouldBe(new[] { "曲线", "直线" });
    }

    /// <summary>回归：判断题的作答必须是中文「正确/错误」，不能是 true/false。</summary>
    [Theory]
    [InlineData("true", "正确")]
    [InlineData("True", "正确")]
    [InlineData("对", "正确")]
    [InlineData("正确", "正确")]
    [InlineData("false", "错误")]
    [InlineData("错", "错误")]
    [InlineData("错误", "错误")]
    public void 判断题作答_应写成中文正确或错误(string answer, string expected)
    {
        var exercise = new Exercise(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "设计表现是设计师将创意构思可视化的重要手段。",
            "设计表现是设计师将创意构思可视化的重要手段。（判断对错）",
            ExerciseType.TrueFalse,
            answer)
        {
            Options = "[\"A. 正确\",\"B. 错误\"]",
        };

        PaperTemplateGenerator.AnswerText(exercise).ShouldBe(expected);
    }

    /// <summary>回归：判断题答案是选项字母时，按选项内容翻译成中文。</summary>
    [Fact]
    public void 判断题作答是选项字母时_应按选项内容翻译()
    {
        var exercise = new Exercise(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "设计表现是设计师将创意构思可视化的重要手段。",
            "设计表现是设计师将创意构思可视化的重要手段。（判断对错）",
            ExerciseType.TrueFalse,
            "B")
        {
            Options = "[\"A. 正确\",\"B. 错误\"]",
        };

        PaperTemplateGenerator.AnswerText(exercise).ShouldBe("错误");
    }
}
