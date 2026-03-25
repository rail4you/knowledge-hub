using System.ClientModel;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Extensions.AI;
using OpenAI;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

builder.Services.AddHttpClient();
builder.Services.AddLogging();
builder.Services.AddAGUI();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();

var apiKey = builder.Configuration["Qwen:ApiKey"] 
    ?? throw new InvalidOperationException("Qwen:ApiKey is not configured");
var baseUrl = builder.Configuration["Qwen:BaseUrl"] 
    ?? "https://dashscope.aliyuncs.com/compatible-mode/v1";
var model = builder.Configuration["Qwen:Model"] ?? "qwen-plus";

Log.Information("Creating AI agents with model {Model} at {BaseUrl}", model, baseUrl);

var client = new OpenAIClient(
    new ApiKeyCredential(apiKey),
    new OpenAIClientOptions { Endpoint = new Uri(baseUrl) });

var chatClient = client.GetChatClient(model);

var chatAgent = chatClient
    .AsIChatClient()
    .AsAIAgent(
        name: "KnowledgeHubAgent",
        description: "KnowledgeHub智能教育助手，支持课程问答、知识点解释",
        instructions: @"你是 KnowledgeHub 平台的智能教育助手。

你的职责：
- 回答关于课程、知识点的问题
- 帮助学生学习，解释概念
- 推荐学习路径
- 生成练习题（如果用户要求）

回答要求：
1. 回答简洁，专业
2. 如果涉及课程信息，先查询课程数据
3. 使用 Markdown 格式化回答");

var lessonPlanAgent = chatClient
    .AsIChatClient()
    .AsAIAgent(
        name: "LessonPlanAgent",
        description: "教案课件生成助手",
        instructions: @"你是专业的教案设计助手。

你的任务是根据教师输入的课程信息，生成结构化、实用的教案。

教案应包含：
1. 课程标题、学科、年级、课时
2. 明确的教学目标（知识目标、能力目标、情感目标）
3. 教学重难点及解决策略
4. 详细的教学环节设计（导入、新授、练习、总结等）
5. 板书设计、课件内容大纲
6. 作业设计

请确保教案内容：
- 结构清晰，逻辑连贯
- 符合教学规律
- 可操作性强
- 时间分配合理

输出格式：JSON格式的结构化教案数据");

var caseAnalysisAgent = chatClient
    .AsIChatClient()
    .AsAIAgent(
        name: "CaseAnalysisAgent",
        description: "案例分析助手",
        instructions: @"你是专业的案例分析助手。

你的任务是对提供的案例进行多维度分析。

分析框架：
1. 案例背景分析
   - 行业背景
   - 塈例主体
   - 时间线
   - 关键事件

2. 核心问题识别
   - 问题描述
   - 问题成因
   - 影响范围
   - 紧急程度

3. 解决方案建议
   - 短期方案
   - 长期方案
   - 资源需求
   - 风险评估

4. 关键成功因素
5. 经验教训总结

请确保分析：
- 客观全面
- 有据有理
- 逻辑清晰
- 建议可行

输出格式：JSON格式的结构化分析报告");

var careerGuidanceAgent = chatClient
    .AsIChatClient()
    .AsAIAgent(
        name: "CareerGuidanceAgent",
        description: "职业规划指导助手",
        instructions: @"你是专业的职业规划指导顾问。

你的职责是根据学生的个人档案，提供个性化的职业发展建议。

你需要了解的信息：
1. 学生的专业背景和成绩
2. 学生的技能和兴趣
3. 学生的职业目标
4. 行业发展趋势
5. 岗位需求情况

建议内容应包括：
1. 个人优势分析
2. 适合的职业方向（3-5个）
3. 技能提升建议
4. 学习路径规划
5. 短期行动计划（3-6个月）
6. 长期发展规划（1-3年）

请确保建议：
- 基于学生实际情况
- 考虑市场需求
- 具体可操作
- 有鼓励性

输出格式：JSON格式的职业规划报告");

app.MapAGUI("/", chatAgent);
app.MapAGUI("/lesson-plan", lessonPlanAgent);
app.MapAGUI("/case-analysis", caseAnalysisAgent);
app.MapAGUI("/career-guidance", careerGuidanceAgent);

app.MapGet("/info", () => Results.Json(new
{
    agents = new[]
    {
        new { name = "KnowledgeHubAgent", description = "KnowledgeHub AI Assistant", instructions = "You are a helpful AI assistant for the KnowledgeHub educational platform." },
        new { name = "LessonPlanAgent", description = "Lesson Plan Generator", instructions = "You are a professional lesson plan and teaching material design assistant." },
        new { name = "CaseAnalysisAgent", description = "Case Study Analysis Assistant", instructions = "You are a professional case study analysis assistant." },
        new { name = "CareerGuidanceAgent", description = "Career Guidance Counselor", instructions = "You are a professional career guidance counselor." }
    }
}));

Log.Information("AG-UI server starting on http://localhost:5001");
app.Run("http://localhost:5001");
