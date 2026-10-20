using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Courses.Enums;
using KnowledgeHub.Exams;
using KnowledgeHub.Exams.Enums;
using KnowledgeHub.Learning;
using KnowledgeHub.Learning.Enums;
using KnowledgeHub.LearningAnalytics.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>
/// 智学分析「示例数据」播种器（默认只在 <b>qidi</b> 租户执行，可用
/// <c>LearningAnalytics:DemoTenantName</c> 改）。
///
/// 覆盖完整链路所需的全部数据：
/// 1) 前置课程《计算机网络技术基础》（4 章 / 12 题）+ 后续课程《网络安全技术》（3 章 / 6 题）；
/// 2) 两个示例学生账号（可直接登录体验学生端）；
/// 3) 已确认的期末试卷批次（2 份试卷 + 单题得分 + 纸质作答记录）；
/// 4) 若干在线作答记录（验证「在线 + 纸质」合并算掌握度）；
/// 5) 课程前后关联（前置 → 后续），用于学习路径与衔接复习包。
///
/// 知识点与习题映射不在此创建：首次调用智学分析接口时会由
/// <c>KnowledgePointIndexer</c> 按章节自动初始化（与真实使用一致）。
///
/// 幂等：若该租户已存在同名课程则整体跳过。
/// </summary>
public class LearningAnalyticsDemoDataSeeder : IDataSeedContributor, ITransientDependency
{
    public const string DemoCourseTitle = "计算机网络技术基础";
    public const string DemoSuccessorCourseTitle = "网络安全技术";
    public const string DemoStudentUserName = "demo_student";
    public const string DemoStudentUserName2 = "demo_student2";
    public const string DemoPassword = "1q2w3E*";

    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;
    private readonly IRepository<Exercise, Guid> _exerciseRepository;
    private readonly IRepository<StudentCourse, Guid> _studentCourseRepository;
    private readonly IRepository<StudentExerciseRecord> _onlineRecordRepository;
    private readonly IRepository<PaperBatch, Guid> _batchRepository;
    private readonly IRepository<PaperScript, Guid> _scriptRepository;
    private readonly IRepository<PaperItemScore, Guid> _itemRepository;
    private readonly IRepository<AnswerRecord, Guid> _answerRepository;
    private readonly IRepository<CourseLink, Guid> _courseLinkRepository;
    private readonly IRepository<AnalysisReport, Guid> _reportRepository;
    private readonly IRepository<StudentNotification, Guid> _notificationRepository;

    private readonly IdentityUserManager _userManager;
    private readonly IIdentityRoleRepository _roleRepository;
    private readonly IIdentityUserRepository _userRepository;

    private readonly IGuidGenerator _guidGenerator;
    private readonly ITenantStore _tenantStore;
    private readonly ICurrentTenant _currentTenant;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LearningAnalyticsDemoDataSeeder> _logger;

    public LearningAnalyticsDemoDataSeeder(
        IRepository<Course, Guid> courseRepository,
        IRepository<Chapter, Guid> chapterRepository,
        IRepository<Exercise, Guid> exerciseRepository,
        IRepository<StudentCourse, Guid> studentCourseRepository,
        IRepository<StudentExerciseRecord> onlineRecordRepository,
        IRepository<PaperBatch, Guid> batchRepository,
        IRepository<PaperScript, Guid> scriptRepository,
        IRepository<PaperItemScore, Guid> itemRepository,
        IRepository<AnswerRecord, Guid> answerRepository,
        IRepository<CourseLink, Guid> courseLinkRepository,
        IRepository<AnalysisReport, Guid> reportRepository,
        IRepository<StudentNotification, Guid> notificationRepository,
        IdentityUserManager userManager,
        IIdentityRoleRepository roleRepository,
        IIdentityUserRepository userRepository,
        IGuidGenerator guidGenerator,
        ITenantStore tenantStore,
        ICurrentTenant currentTenant,
        IConfiguration configuration,
        ILogger<LearningAnalyticsDemoDataSeeder> logger)
    {
        _courseRepository = courseRepository;
        _chapterRepository = chapterRepository;
        _exerciseRepository = exerciseRepository;
        _studentCourseRepository = studentCourseRepository;
        _onlineRecordRepository = onlineRecordRepository;
        _batchRepository = batchRepository;
        _scriptRepository = scriptRepository;
        _itemRepository = itemRepository;
        _answerRepository = answerRepository;
        _courseLinkRepository = courseLinkRepository;
        _reportRepository = reportRepository;
        _notificationRepository = notificationRepository;
        _userManager = userManager;
        _roleRepository = roleRepository;
        _userRepository = userRepository;
        _guidGenerator = guidGenerator;
        _tenantStore = tenantStore;
        _currentTenant = currentTenant;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (!context.TenantId.HasValue)
        {
            return;
        }

        var targetTenantName = _configuration["LearningAnalytics:DemoTenantName"] ?? "qidi";
        var tenant = await _tenantStore.FindAsync(context.TenantId.Value);
        if (tenant == null || !string.Equals(tenant.Name, targetTenantName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (await _courseRepository.AnyAsync(c => c.Title == DemoCourseTitle))
        {
            _logger.LogInformation("智学分析示例数据已存在（租户 {Tenant}），跳过播种", tenant.Name);
            return;
        }

        _logger.LogInformation("开始为租户 {Tenant} 播种智学分析示例数据…", tenant.Name);

        var predecessor = await SeedCourseAsync(
            DemoCourseTitle,
            "计算机网络技术基础（示例课程）：面向计算机类专业的入门课程，覆盖网络体系结构、IP 地址与子网划分、传输层协议与网络设备组网。",
            chapters: new[]
            {
                "OSI 与 TCP/IP 分层模型",
                "IP 地址与子网划分",
                "TCP 与 UDP 协议",
                "交换机与路由器组网",
            },
            exercises: new (string Chapter, string Title, string Content, ExerciseType Type, string? Options, string Answer, string? Analysis, int Score, int Difficulty)[]
            {
                ("OSI 与 TCP/IP 分层模型", "OSI 参考模型分层数量", "OSI 参考模型共分为几层？",
                    ExerciseType.SingleChoice, Options("A", "4 层", "B", "5 层", "C", "6 层", "D", "7 层"), "D",
                    "OSI 参考模型自下而上分为物理层、数据链路层、网络层、传输层、会话层、表示层、应用层，共 7 层。", 5, 1),
                ("OSI 与 TCP/IP 分层模型", "TCP/IP 模型与 OSI 的对应关系", "TCP/IP 模型中的网络接口层对应 OSI 模型的物理层和数据链路层。",
                    ExerciseType.TrueFalse, null, "true",
                    "TCP/IP 的网络接口层（网络访问层）覆盖 OSI 的物理层与数据链路层。", 5, 2),
                ("OSI 与 TCP/IP 分层模型", "端到端可靠传输所在层", "OSI 七层模型中，负责端到端可靠传输的是 ______ 层。",
                    ExerciseType.FillBlank, null, "传输",
                    "传输层（TCP）提供面向连接的、可靠的端到端数据传输。", 5, 2),

                ("IP 地址与子网划分", "C 类网段可用主机数", "192.168.1.0/24 网段最多可分配的可用主机地址数是？",
                    ExerciseType.SingleChoice, Options("A", "254", "B", "255", "C", "256", "D", "253"), "A",
                    "/24 共 256 个地址，去掉网络地址与广播地址，可用主机地址 254 个。", 5, 2),
                ("IP 地址与子网划分", "等长子网划分掩码计算", "将 192.168.10.0/24 划分为 4 个等长子网，每个子网的子网掩码是多少？",
                    ExerciseType.Calculation, null, "255.255.255.192",
                    "划分 4 个子网需要借 2 位主机位，掩码由 /24 变为 /26，即 255.255.255.192。", 10, 3),
                ("IP 地址与子网划分", "私有地址识别", "下列哪个地址属于私有地址？",
                    ExerciseType.SingleChoice, Options("A", "8.8.8.8", "B", "172.16.5.4", "C", "100.1.1.1", "D", "11.0.0.1"), "B",
                    "私有地址范围为 10.0.0.0/8、172.16.0.0/12、192.168.0.0/16，172.16.5.4 属于私有地址。", 5, 1),
                ("IP 地址与子网划分", "/26 子网地址数量", "子网掩码 /26 表示每个子网包含 64 个 IP 地址。",
                    ExerciseType.TrueFalse, null, "true",
                    "主机位为 32-26=6 位，2^6=64 个地址。", 5, 2),

                ("TCP 与 UDP 协议", "TCP 三次握手", "TCP 建立连接需要几次握手？",
                    ExerciseType.SingleChoice, Options("A", "1 次", "B", "2 次", "C", "3 次", "D", "4 次"), "C",
                    "TCP 通过 SYN、SYN+ACK、ACK 三次握手建立连接。", 5, 1),
                ("TCP 与 UDP 协议", "UDP 协议特点", "UDP 协议的特点是面向 ______、不保证可靠交付、开销小。",
                    ExerciseType.FillBlank, null, "无连接",
                    "UDP 是无连接协议，不保证顺序与可靠性，适合实时音视频等场景。", 5, 2),
                ("TCP 与 UDP 协议", "TCP 流量控制机制", "TCP 使用滑动窗口机制进行流量控制。",
                    ExerciseType.TrueFalse, null, "true",
                    "TCP 通过接收窗口（滑动窗口）实现流量控制，避免发送方压垮接收方。", 5, 2),

                ("交换机与路由器组网", "交换机工作层次", "交换机主要工作在 OSI 参考模型的哪一层？",
                    ExerciseType.SingleChoice, Options("A", "物理层", "B", "数据链路层", "C", "网络层", "D", "传输层"), "B",
                    "二层交换机依据 MAC 地址转发帧，工作在数据链路层；三层交换机才具备路由能力。", 5, 1),
                ("交换机与路由器组网", "路由器与交换机区别", "简述路由器与交换机的主要区别。",
                    ExerciseType.ShortAnswer, null,
                    "路由器工作在网络层，依据 IP 地址转发并实现不同网段互连；交换机工作在数据链路层，依据 MAC 地址在同一网段内转发。",
                    "从工作层次、转发依据、隔离广播域能力三方面回答即可。", 10, 3),
            });

        var successor = await SeedCourseAsync(
            DemoSuccessorCourseTitle,
            "网络安全技术（示例后续课程）：在计算机网络基础之上，介绍网络安全体系、密码学基础与防火墙/入侵检测技术。",
            chapters: new[]
            {
                "网络安全威胁与防护",
                "对称与非对称加密",
                "防火墙与入侵检测",
            },
            exercises: new (string Chapter, string Title, string Content, ExerciseType Type, string? Options, string Answer, string? Analysis, int Score, int Difficulty)[]
            {
                ("对称与非对称加密", "对称加密算法识别", "以下属于对称加密算法的是？",
                    ExerciseType.SingleChoice, Options("A", "RSA", "B", "AES", "C", "ECDSA", "D", "SHA-256"), "B",
                    "AES 是对称加密；RSA、ECDSA 为非对称加密；SHA-256 是哈希算法。", 5, 1),
                ("网络安全威胁与防护", "HTTPS 传输安全", "HTTPS 使用 TLS 协议保证传输过程中的机密性与完整性。",
                    ExerciseType.TrueFalse, null, "true",
                    "HTTPS = HTTP over TLS，提供加密、完整性校验与身份认证。", 5, 1),
                ("防火墙与入侵检测", "防火墙默认策略", "防火墙的默认策略应遵循哪一原则？",
                    ExerciseType.SingleChoice, Options("A", "全部允许", "B", "最小权限", "C", "随机放行", "D", "按 IP 长度放行"), "B",
                    "默认拒绝、按需放行（最小权限原则）是防火墙配置的基本要求。", 5, 2),
                ("对称与非对称加密", "数字签名的作用", "数字签名主要保证数据的完整性和 ______ 性。",
                    ExerciseType.FillBlank, null, "不可否认",
                    "数字签名使用私钥签名、公钥验证，可保证完整性、身份真实性与不可否认性。", 5, 3),
                ("防火墙与入侵检测", "入侵检测系统缩写", "用于检测网络入侵行为的系统缩写是？",
                    ExerciseType.SingleChoice, Options("A", "IDS", "B", "DNS", "C", "DHCP", "D", "NAT"), "A",
                    "IDS（Intrusion Detection System）负责检测入侵行为，IPS 还可主动阻断。", 5, 1),
                ("对称与非对称加密", "对称与非对称加密对比", "简述对称加密与非对称加密的区别及适用场景。",
                    ExerciseType.ShortAnswer, null,
                    "对称加密加解密使用同一密钥，速度快，适合大数据量加密（如 AES）；非对称加密使用公钥/私钥对，速度慢，适合密钥交换与数字签名（如 RSA）。",
                    "从密钥数量、性能、典型算法与适用场景四方面作答。", 10, 3),
            });

        // 把示例课程挂到租户内第一个 Teacher 账号上：便于演示教师端「只看我教的课程」过滤
        var teacher = await FindFirstTeacherAsync();
        if (teacher != null)
        {
            predecessor.TeacherId = teacher.Id;
            successor.TeacherId = teacher.Id;
            await _courseRepository.UpdateAsync(predecessor, autoSave: true);
            await _courseRepository.UpdateAsync(successor, autoSave: true);
        }

        // 课程前后关联（前置 → 后续）
        await _courseLinkRepository.InsertAsync(new CourseLink(
            _guidGenerator.Create(), predecessor.Id, successor.Id)
        {
            Status = PrerequisiteStatus.Confirmed,
        }, autoSave: true);

        // 示例学生（可登录体验学生端）
        var zhangChen = await EnsureDemoStudentAsync(DemoStudentUserName, "张晨", "学生", "2023010101", "计算机应用技术 2301 班");
        var liSiyu = await EnsureDemoStudentAsync(DemoStudentUserName2, "李思雨", "学生", "2023010102", "计算机应用技术 2301 班");

        await EnrollAsync(zhangChen.Id, predecessor.Id, StudentCourseStatus.InProgress, 35);
        await EnrollAsync(liSiyu.Id, predecessor.Id, StudentCourseStatus.InProgress, 45);
        // 后续课程：张晨已选课，用于演示「学习路径 + 衔接复习包」
        await EnrollAsync(zhangChen.Id, successor.Id, StudentCourseStatus.Enrolled, 0);

        // 期末试卷（已确认，含单题得分与纸质作答记录）
        await SeedPaperAsync(predecessor, zhangChen, liSiyu);

        // 在线作答（验证「在线 + 纸质」合并计算掌握度；最近几条含错题，便于演示推荐离线评估）
        await SeedOnlineRecordsAsync(predecessor, zhangChen, new[] { 1.0, 0.0, 0.5, 1.0, 0.0, 0.0 });
        await SeedOnlineRecordsAsync(predecessor, liSiyu, new[] { 0.0, 1.0, 0.0, 0.5, 1.0, 0.0 });

        _logger.LogInformation(
            "智学分析示例数据播种完成：前置课程 {Pre} / 后续课程 {Succ}，学生 {S1}、{S2}（密码 {Password}）",
            predecessor.Title, successor.Title, DemoStudentUserName, DemoStudentUserName2, DemoPassword);
    }

    // ══════════════════════════════════════════════════════════
    //  课程 / 章节 / 习题
    // ══════════════════════════════════════════════════════════

    private async Task<Course> SeedCourseAsync(
        string title,
        string description,
        string[] chapters,
        (string Chapter, string Title, string Content, ExerciseType Type, string? Options, string Answer, string? Analysis, int Score, int Difficulty)[] exercises)
    {
        var course = new Course(_guidGenerator.Create(), title)
        {
            Description = description,
            Semester = "2026春",
            Credits = 4,
            SemesterHours = 64,
            Status = CourseStatus.Published,
            Difficulty = 2,
            IsRecommended = true,
        };
        await _courseRepository.InsertAsync(course, autoSave: true);

        var chapterIndex = new Dictionary<string, Chapter>();
        for (var i = 0; i < chapters.Length; i++)
        {
            var chapter = new Chapter(_guidGenerator.Create(), course.Id, chapters[i], i + 1);
            chapter.Description = $"{chapters[i]}（示例章节）";
            await _chapterRepository.InsertAsync(chapter, autoSave: true);
            chapterIndex[chapters[i]] = chapter;
        }

        foreach (var item in exercises)
        {
            var exercise = new Exercise(
                _guidGenerator.Create(),
                course.Id,
                item.Title,
                item.Content,
                item.Type,
                item.Answer)
            {
                ChapterId = chapterIndex[item.Chapter].Id,
                Options = item.Options,
                QuestionAnalysis = item.Analysis,
                AnswerExplanation = item.Analysis,
                Score = item.Score,
                Difficulty = item.Difficulty,
            };
            await _exerciseRepository.InsertAsync(exercise, autoSave: true);
        }

        _logger.LogInformation("示例课程已创建：{Title}（{Chapters} 章 / {Exercises} 题）",
            title, chapters.Length, exercises.Length);
        return course;
    }

    /// <summary>选项 JSON：[{key,content}]，与前端 parseOptions 兼容。</summary>
    private static string Options(params string[] pairs)
    {
        var items = new List<string>();
        for (var i = 0; i + 1 < pairs.Length; i += 2)
        {
            items.Add($"{{\"key\":\"{pairs[i]}\",\"content\":\"{pairs[i + 1]}\"}}");
        }

        return "[" + string.Join(",", items) + "]";
    }

    // ══════════════════════════════════════════════════════════
    //  学生 / 选课
    // ══════════════════════════════════════════════════════════

    /// <summary>找一个租户内的 Teacher 账号（没有就跳过，不阻塞播种）。</summary>
    private async Task<IdentityUser?> FindFirstTeacherAsync()
    {
        try
        {
            var users = await _userRepository.GetListByNormalizedRoleNameAsync("TEACHER");
            return users.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "查找 Teacher 账号失败，示例课程不设置授课教师");
            return null;
        }
    }

    private async Task<IdentityUser> EnsureDemoStudentAsync(
        string userName, string name, string surname, string studentNumber, string grade)
    {
        var existing = await _userManager.FindByNameAsync(userName);
        if (existing != null)
        {
            return existing;
        }

        // 关键：租户必须通过构造函数传入（TenantId 是 protected set），否则会建成宿主用户
        var user = new IdentityUser(_guidGenerator.Create(), userName, $"{userName}@demo.local", _currentTenant.Id);
        user.Name = name;
        user.Surname = surname;
        user.SetEmailConfirmed(true);
        // 学号放扩展属性（避免当手机号校验），学生端/报表里可读取
        user.ExtraProperties["StudentNumber"] = studentNumber;
        user.ExtraProperties["Grade"] = grade;

        var createResult = await _userManager.CreateAsync(user, DemoPassword);
        if (!createResult.Succeeded)
        {
            _logger.LogWarning("创建示例学生 {UserName} 失败：{Errors}", userName,
                string.Join(";", createResult.Errors.Select(e => e.Description)));
            return existing ?? user;
        }

        var studentRole = await _roleRepository.FindByNormalizedNameAsync("STUDENT");
        if (studentRole != null)
        {
            await _userManager.AddToRoleAsync(user, studentRole.Name!);
        }

        _logger.LogInformation("示例学生已创建：{UserName}（{Name}，学号 {StudentNumber}）", userName, name, studentNumber);
        return user;
    }

    private async Task EnrollAsync(Guid studentId, Guid courseId, StudentCourseStatus status, decimal progress)
    {
        var exists = await _studentCourseRepository.AnyAsync(x => x.StudentId == studentId && x.CourseId == courseId);
        if (exists)
        {
            return;
        }

        var enrollment = new StudentCourse(_guidGenerator.Create(), studentId, courseId);
        if (status != StudentCourseStatus.Enrolled)
        {
            enrollment.StartLearning();
        }

        if (progress > 0)
        {
            enrollment.SetResourceProgress(progress);
        }

        await _studentCourseRepository.InsertAsync(enrollment, autoSave: true);
    }

    // ══════════════════════════════════════════════════════════
    //  试卷（已确认）+ 纸质作答
    // ══════════════════════════════════════════════════════════

    private async Task SeedPaperAsync(Course course, IdentityUser zhangChen, IdentityUser liSiyu)
    {
        var examDate = DateTime.UtcNow.AddDays(-21);
        var batch = new PaperBatch(_guidGenerator.Create(), course.Id, course.Title, "2026春 期末考试（示例）")
        {
            ExamDate = examDate,
            Status = PaperBatchStatus.Confirmed,
            IsMockOcr = false,
            SourceFileNames = "demo-paper.pdf",
        };
        await _batchRepository.InsertAsync(batch, autoSave: true);

        var exercises = (await _exerciseRepository.GetListAsync(e => e.CourseId == course.Id))
            .OrderBy(e => e.CreationTime)
            .ToList();

        // 张晨：偏弱；李思雨：中等偏上
        var zhangRatios = new[] { 0.2, 0.6, 0.2, 0.4, 0.2, 0.8, 0.6, 0.6, 0.4, 0.8, 0.8, 0.4 };
        var liRatios = new[] { 0.8, 1.0, 0.8, 0.6, 0.8, 1.0, 0.8, 0.8, 0.8, 1.0, 0.8, 0.6 };

        var scriptZhang = await SeedScriptAsync(batch, course, exercises, zhangChen, "2023010101", "张晨", zhangRatios, examDate);
        var scriptLi = await SeedScriptAsync(batch, course, exercises, liSiyu, "2023010102", "李思雨", liRatios, examDate);

        batch.ScriptCount = 2;
        batch.NeedsConfirmCount = 0;
        batch.AverageScore = Math.Round((scriptZhang.TotalScore + scriptLi.TotalScore) / 2, 1);
        batch.PushedCount = 2;
        await _batchRepository.UpdateAsync(batch, autoSave: true);

        // 预置「个人薄弱点报告 + 站内消息」，让学生端首次登录就能看到推送效果
        await SeedStudentPushAsync(batch, course, scriptZhang, zhangChen, "张晨", zhangRatios, exercises, examDate);
        await SeedStudentPushAsync(batch, course, scriptLi, liSiyu, "李思雨", liRatios, exercises, examDate);
    }

    /// <summary>生成个人报告与站内消息（内容为规则文案，不调用模型）。</summary>
    private async Task SeedStudentPushAsync(
        PaperBatch batch,
        Course course,
        PaperScript script,
        IdentityUser student,
        string studentName,
        double[] ratios,
        List<Exercise> exercises,
        DateTime examDate)
    {
        var chapterIds = exercises.Where(e => e.ChapterId.HasValue).Select(e => e.ChapterId!.Value).Distinct().ToList();
        var chapters = chapterIds.Any()
            ? (await _chapterRepository.GetListAsync(c => chapterIds.Contains(c.Id))).ToDictionary(c => c.Id, c => c.Title)
            : new Dictionary<Guid, string>();

        // 按章节聚合得分率，取最薄弱的两个
        var byChapter = exercises
            .Select((e, i) => new
            {
                Title = e.ChapterId.HasValue ? chapters.GetValueOrDefault(e.ChapterId.Value, "课程综合") : "课程综合",
                Ratio = i < ratios.Length ? ratios[i] : 0.6,
            })
            .GroupBy(x => x.Title)
            .Select(g => new { Title = g.Key, Ratio = g.Average(x => x.Ratio) })
            .OrderBy(x => x.Ratio)
            .ToList();

        var weakest = byChapter.Take(2).ToList();
        var weakestText = string.Join("」「", weakest.Select(w => w.Title));
        var summary = $"{studentName} 本次考试得分 {script.TotalScore:0.#}（班级平均 {batch.AverageScore:0.#}），" +
                      $"{byChapter.Count(w => w.Ratio < 0.75)} 个知识模块未达教学目标，" +
                      $"建议优先复习「{weakestText}」。";

        // 用 JsonSerializer 生成结构化内容，避免手写 JSON 转义
        var contentJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            Summary = summary,
            WeakPoints = weakest.Select(w => new
            {
                Name = w.Title,
                Reason = $"掌握率 {(int)Math.Round(w.Ratio * 100)}%，低于学习目标",
                Advice = $"回看「{w.Title}」相关讲解，并完成 5 道同类练习",
            }).ToList(),
            StudyPlan = new[]
            {
                $"先复习「{weakest.FirstOrDefault()?.Title}」，再做专项练习",
                "完成后回到「智学分析」查看掌握率变化",
            },
        });

        var factsJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            score = script.TotalScore,
            classAverage = batch.AverageScore,
            weakCount = byChapter.Count(w => w.Ratio < 0.75),
        });

        var report = new AnalysisReport(_guidGenerator.Create(), AnalysisScope.Student, course.Id)
        {
            StudentId = student.Id,
            PaperBatchId = batch.Id,
            Title = $"{batch.Title} · 个人薄弱点报告",
            Summary = summary,
            ContentJson = contentJson,
            FactsJson = factsJson,
            GeneratedByLlm = false,
            GeneratedAt = examDate,
            PushedAt = examDate,
        };
        await _reportRepository.InsertAsync(report, autoSave: true);

        var notification = new StudentNotification(
            _guidGenerator.Create(),
            student.Id,
            $"【成绩分析】{batch.Title} 薄弱点报告已生成",
            summary)
        {
            Type = AnalysisNotificationType.PaperWeakness,
            Source = "班主任",
            CourseId = course.Id,
            RelatedId = report.Id,
        };
        await _notificationRepository.InsertAsync(notification, autoSave: true);
    }

    private async Task<PaperScript> SeedScriptAsync(
        PaperBatch batch,
        Course course,
        List<Exercise> exercises,
        IdentityUser student,
        string studentNumber,
        string studentName,
        double[] ratios,
        DateTime answeredAt)
    {
        var script = new PaperScript(_guidGenerator.Create(), batch.Id, studentNumber, studentName)
        {
            StudentId = student.Id,
            OcrConfidence = 0.96,
            OcrProvider = "demo",
            OcrModel = "demo-v1",
            Confirmed = true,
            ConfirmedAt = answeredAt,
            SourceFileName = "demo-paper.pdf",
        };

        double total = 0;
        for (var i = 0; i < exercises.Count; i++)
        {
            var exercise = exercises[i];
            var fullScore = exercise.Score > 0 ? exercise.Score : 5;
            var ratio = i < ratios.Length ? ratios[i] : 0.6;
            var score = Math.Round(fullScore * ratio * 2, MidpointRounding.AwayFromZero) / 2;
            total += score;

            await _itemRepository.InsertAsync(new PaperItemScore(
                _guidGenerator.Create(), script.Id, batch.Id, (i + 1).ToString())
            {
                ExerciseId = exercise.Id,
                ChapterId = exercise.ChapterId,
                MatchStatus = PaperMatchStatus.Matched,
                Score = score,
                FullScore = fullScore,
                OcrConfidence = 0.95,
                RawText = exercise.Title,
            }, autoSave: true);

            // 校对确认后的纸质作答记录（掌握度只认这里 + 在线作答）
            await _answerRepository.InsertAsync(new AnswerRecord(
                _guidGenerator.Create(), student.Id, course.Id, exercise.Id)
            {
                ChapterId = exercise.ChapterId,
                Score = score,
                FullScore = fullScore,
                Source = AnswerSource.Paper,
                AnsweredAt = answeredAt,
                PaperScriptId = script.Id,
            }, autoSave: true);
        }

        script.TotalScore = total;
        await _scriptRepository.InsertAsync(script, autoSave: true);
        return script;
    }

    // ══════════════════════════════════════════════════════════
    //  在线作答（与纸质数据合并）
    // ══════════════════════════════════════════════════════════

    private async Task SeedOnlineRecordsAsync(Course course, IdentityUser student, double[] ratios)
    {
        var exercises = (await _exerciseRepository.GetListAsync(e => e.CourseId == course.Id))
            .OrderBy(e => e.CreationTime)
            .Take(ratios.Length)
            .ToList();

        for (var i = 0; i < exercises.Count; i++)
        {
            var exercise = exercises[i];
            var ratio = ratios[i];
            var correct = ratio >= 0.9;

            // 非选择题用「与标准答案不同」的字符串模拟错误作答
            var answer = correct
                ? exercise.Answer
                : (exercise.Type == ExerciseType.SingleChoice || exercise.Type == ExerciseType.MultiChoice
                    ? "Z"
                    : "未作答");

            var record = new StudentExerciseRecord(_guidGenerator.Create(), student.Id, course.Id, exercise.Id)
            {
                ChapterId = exercise.ChapterId,
                TimeSpent = TimeSpan.FromMinutes(2 + i),
            };
            record.SetAnswer(answer, correct);
            record.MarkAnswerViewed();

            // 显式指定完成时间：i 越大越新（最后 3 条包含错题，供离线评估使用）
            record.CompletedAt = DateTime.UtcNow.AddDays(-(ratios.Length - i));
            await _onlineRecordRepository.InsertAsync(record, autoSave: true);
        }
    }
}
