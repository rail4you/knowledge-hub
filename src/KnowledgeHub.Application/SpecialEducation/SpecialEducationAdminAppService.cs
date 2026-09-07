using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Courses.Enums;
using KnowledgeHub.Features;
using KnowledgeHub.Learning;
using KnowledgeHub.Permissions;
using KnowledgeHub.SpecialEducation;
using KnowledgeHub.SpecialEducation.Dtos;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.FeatureManagement;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Users;
using IdentityUser = Volo.Abp.Identity.IdentityUser;

namespace KnowledgeHub.Application.SpecialEducation;

[Authorize(KnowledgeHubPermissions.SpecialEducation.Manage)]
public class SpecialEducationAdminAppService : KnowledgeHubAppService, ISpecialEducationAdminAppService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IFeatureManager _featureManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataSeeder _dataSeeder;
    private readonly IRepository<SpecialTeachingDesign, Guid> _designRepository;
    private readonly IRepository<IepPlan, Guid> _iepRepository;
    private readonly IRepository<SpecialEduResource, Guid> _resourceRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<StudentCourse, Guid> _enrollmentRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly ICurrentUser _currentUser;

    public SpecialEducationAdminAppService(
        ITenantRepository tenantRepository,
        IFeatureManager featureManager,
        ICurrentTenant currentTenant,
        IDataSeeder dataSeeder,
        IRepository<SpecialTeachingDesign, Guid> designRepository,
        IRepository<IepPlan, Guid> iepRepository,
        IRepository<SpecialEduResource, Guid> resourceRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<StudentCourse, Guid> enrollmentRepository,
        IRepository<IdentityUser, Guid> userRepository,
        ICurrentUser currentUser)
    {
        _tenantRepository = tenantRepository;
        _featureManager = featureManager;
        _currentTenant = currentTenant;
        _dataSeeder = dataSeeder;
        _designRepository = designRepository;
        _iepRepository = iepRepository;
        _resourceRepository = resourceRepository;
        _courseRepository = courseRepository;
        _enrollmentRepository = enrollmentRepository;
        _userRepository = userRepository;
        _currentUser = currentUser;
    }

    public async Task<List<SpecialEduTenantStateDto>> GetTenantStatesAsync()
    {
        var tenants = await _tenantRepository.GetListAsync();
        var result = new List<SpecialEduTenantStateDto>();
        foreach (var t in tenants)
        {
            var value = await _featureManager.GetOrNullAsync(KnowledgeHubFeatures.SpecialEducation, "T", t.Id.ToString());
            result.Add(new SpecialEduTenantStateDto
            {
                TenantId = t.Id,
                TenantName = t.Name,
                Enabled = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            });
        }
        return result;
    }

    public async Task SetTenantEnabledAsync(SetSpecialEduTenantEnabledDto input)
    {
        await _featureManager.SetAsync(KnowledgeHubFeatures.SpecialEducation, input.Enabled ? "true" : "false", "T", input.TenantId.ToString());
        // 新开通租户：触发该租户的角色权限自愈，确保 SchoolAdmin/Teacher/Student 拿到特教权限整体包。
        if (input.Enabled)
        {
            using (_currentTenant.Change(input.TenantId))
            {
                await _dataSeeder.SeedAsync();
            }
        }
    }

    /// <summary>
    /// 验证用 Mock 数据（特教方向）：目标租户内幂等写入 1 特教课程 + 2 教案 + 2 IEP + 4 资源，
    /// 并将 zmq 学生选入特教课程。建议在新租户下执行一次，用于教师/学生端验证。
    /// </summary>
    public async Task<SeedMockDataResultDto> SeedMockDataAsync(SeedMockDataInputDto input)
    {
        var targetTenantId = input.TenantId ?? CurrentTenant.Id;
        using (_currentTenant.Change(targetTenantId))
        {
            return await SeedMockDataInCurrentTenantAsync();
        }
    }

    private async Task<SeedMockDataResultDto> SeedMockDataInCurrentTenantAsync()
    {
        var tenantId = _currentTenant.Id;
        var me = _currentUser.GetId();
        var added = new List<string>();

        // 1. 特教示范课程（幂等：按标题查找）
        const string mockCourseTitle = "生活语文·感知与表达（特教示范课程）";
        var mockCourse = (await _courseRepository.GetListAsync(x => x.Title == mockCourseTitle)).FirstOrDefault();
        if (mockCourse == null)
        {
            mockCourse = new Course(Guid.NewGuid(), mockCourseTitle)
            {
                TenantId = tenantId,
                Description = "特殊教育示范课程：培智/听障/视障/孤独症适配教学验证用",
                Status = CourseStatus.Published,
                Difficulty = 1
            };
            await _courseRepository.InsertAsync(mockCourse);
        }

        // 2. zmq 学生选入特教课程（IEP 页下拉可直接选到“已选课”学生）
        var zmq = (await _userRepository.GetListAsync(x => x.UserName == "zmq")).FirstOrDefault();
        if (zmq != null)
        {
            var enrolled = await _enrollmentRepository.AnyAsync(
                x => x.CourseId == mockCourse.Id && x.StudentId == zmq.Id);
            if (!enrolled)
            {
                await _enrollmentRepository.InsertAsync(
                    new StudentCourse(Guid.NewGuid(), zmq.Id, mockCourse.Id));
            }
        }

        // 3. 教案 / IEP / 资源包：按标题幂等补种（老租户重跑只补缺失项）
        var d1Id = await EnsureDesignAsync(tenantId, me, mockCourse.Id,
            "《认识水果》——培智生活语文（示范）", SpecialEduCategory.Peizhi, added,
            subject: "生活语文", grade: "培智三年级",
            objectives: new[] { "能指认苹果、香蕉、橙子三种水果", "能在提示下说出水果颜色", "养成洗手后进食的卫生习惯" },
            keyPoints: new[] { "水果名称与颜色配对" }, difficulties: new[] { "从图片泛化到实物" },
            board: new[] { "左侧：水果图片区", "右侧：颜色配对区" },
            basis: "依据培智学校义务教育课程标准（生活语文）：以生活化、直观化为原则。");
        var d2Id = await EnsureDesignAsync(tenantId, me, null,
            "《轮流玩积木》——孤独症社交沟通（示范）", SpecialEduCategory.Guzuzheng, added,
            subject: "社交沟通", grade: "孤独症支持班",
            objectives: new[] { "能用视觉卡表达“轮到我”", "等待时间达到1分钟", "成功完成2次轮流" },
            keyPoints: new[] { "轮流概念与表达" }, difficulties: new[] { "等待时的情绪调节" },
            board: new[] { "视觉日程条", "轮流提示卡" },
            basis: "依据孤独症教育结构化教学规范：可预测流程+视觉支持。");

        var studentId = zmq?.Id ?? me;
        var studentName = zmq != null ? (!zmq.Name.IsNullOrEmpty() ? zmq.Name! : "zmq") : "测试学生";
        var iep1Id = await EnsureIepAsync(tenantId, studentId,
            zmq != null ? $"{studentName}（听障示范）" : "测试学生（听障示范）",
            SpecialEduCategory.TingZhang, mockCourse.Id, added,
            profile: "听力损失60dB，视觉学习优势，已选《生活语文·感知与表达》课程。",
            longGoals: new[] { "本学期掌握50个常用书面词汇" },
            shortGoals: new[] { "4周内指认20个水果/食物词汇（正确率≥80%）" },
            strategies: new[] { "视觉卡+手语辅助", "小组配对练习" },
            evaluation: new[] { "每周词汇指认记录表" },
            homeSchool: new[] { "家长每日15分钟卡片复习", "每周五沟通本反馈" });
        var iep2Id = await EnsureIepAsync(tenantId, studentId,
            zmq != null ? $"{studentName}（视障示范）" : "测试学生（视障示范）",
            SpecialEduCategory.ShiZhang, mockCourse.Id, added,
            profile: "低视力，听觉触觉优势，已选《生活语文·感知与表达》课程，定向行走需支持。",
            longGoals: new[] { "独立完成校园定向行走" },
            shortGoals: new[] { "4周内口述描述教室布局" },
            strategies: new[] { "口述影像+触觉地图" },
            evaluation: new[] { "定向行走观察量表" },
            homeSchool: new[] { "家庭触觉标识布置", "每日口述复述" });

        await EnsureResourceAsync(tenantId, me, "社交故事：轮流玩（示范）",
            SpecialEduCategory.Guzuzheng, SpecialEduResourceModality.SocialStory, d2Id, null,
            new[] { "今天我要和同学一起玩积木。", "轮到我时，我说“轮到我了”。", "等待时我可以数到10。" }, added);
        await EnsureResourceAsync(tenantId, me, "视觉支持：洗手步骤卡（示范）",
            SpecialEduCategory.Peizhi, SpecialEduResourceModality.VisualSupport, d1Id, null,
            new[] { "1. 开水龙头", "2. 打肥皂", "3. 搓手20秒", "4. 冲洗擦干" }, added);
        await EnsureResourceAsync(tenantId, me, "视频脚本：水果词汇课（示范）",
            SpecialEduCategory.TingZhang, SpecialEduResourceModality.VideoScript, d1Id, null,
            new[] { "分镜1：特写苹果+字幕+手语框（10s）", "分镜2：学生跟做指认（15s）" }, added);
        await EnsureResourceAsync(tenantId, me, "音频脚本：教室定向口述（示范）",
            SpecialEduCategory.ShiZhang, SpecialEduResourceModality.AudioScript, null, iep2Id,
            new[] { "旁白：从门口出发，向左三步是讲台……（慢速，重复2遍）" }, added);

        // 4. 盲文对照 Mock（现行盲文·不标调，分词连写）：始终确保存在
        await EnsureBrailleMockAsync(tenantId, me, mockCourse.Id, added);

        return new SeedMockDataResultDto
        {
            TeachingDesignCount = await _designRepository.CountAsync(),
            IepCount = await _iepRepository.CountAsync(),
            ResourceCount = await _resourceRepository.CountAsync(),
            Message = added.Count == 0
                ? "Mock 数据均已存在，无需补种。"
                : $"Mock 补种完成：{string.Join("、", added)}。"
        };
    }

    private async Task<Guid?> EnsureDesignAsync(Guid? tenantId, Guid creator, Guid? courseId,
        string title, SpecialEduCategory category, List<string> added,
        string subject = "生活语文", string grade = "特教支持班",
        string[]? objectives = null, string[]? keyPoints = null, string[]? difficulties = null,
        string[]? board = null, string? basis = null)
    {
        var existing = (await _designRepository.GetListAsync(x => x.Title == title)).FirstOrDefault();
        if (existing != null) return existing.Id;
        objectives ??= new[] { "示范目标1", "示范目标2" };
        keyPoints ??= new[] { "示范重点" };
        difficulties ??= new[] { "示范难点" };
        board ??= new[] { "示范板书" };
        var entity = new SpecialTeachingDesign(Guid.NewGuid(), tenantId, creator, category)
        {
            Title = title,
            CourseId = courseId,
            Subject = subject,
            Grade = grade,
            Duration = 40,
            ObjectivesJson = JsonSerializer.Serialize(objectives),
            KeyPointsJson = JsonSerializer.Serialize(keyPoints),
            DifficultiesJson = JsonSerializer.Serialize(difficulties),
            BoardDesignJson = JsonSerializer.Serialize(board),
            StandardBasis = basis ?? "特教课程标准示范数据。",
            RawJson = JsonSerializer.Serialize(new
            {
                title, subject, grade, duration = 40,
                objectives, keyPoints, difficulties,
                sections = new[] { new { name = "导入", duration = 5, content = "示范环节", activities = new[] { "示范活动" } } },
                methods = new[] { "直观演示法" }, resources = new[] { "示范教具" },
                assessment = new[] { "观察记录" }, homework = new[] { "家庭巩固练习" },
                boardDesign = board, slidesOutline = new[] { "示范课件页" },
                activities = new[] { "示范活动" }, assessmentTools = new[] { "示范评估表" },
                standardBasis = basis ?? "特教课程标准示范数据。"
            }),
            Status = SpecialEduPlanStatus.Published
        };
        await _designRepository.InsertAsync(entity);
        added.Add($"教案《{title}》");
        return entity.Id;
    }

    private async Task<Guid?> EnsureIepAsync(Guid? tenantId, Guid studentId, string studentName,
        SpecialEduCategory category, Guid? courseId, List<string> added,
        string profile = "示范现状分析。",
        string[]? longGoals = null, string[]? shortGoals = null, string[]? strategies = null,
        string[]? evaluation = null, string[]? homeSchool = null)
    {
        var existing = (await _iepRepository.GetListAsync(x => x.StudentName == studentName)).FirstOrDefault();
        if (existing != null) return existing.Id;
        longGoals ??= new[] { "示范长期目标" };
        shortGoals ??= new[] { "示范短期目标" };
        strategies ??= new[] { "示范策略" };
        evaluation ??= new[] { "示范评估" };
        homeSchool ??= new[] { "示范家校协同" };
        var entity = new IepPlan(Guid.NewGuid(), tenantId, studentId, category)
        {
            StudentName = studentName,
            CourseId = courseId,
            ProfileJson = JsonSerializer.Serialize(new { summary = profile }),
            LongTermGoalsJson = JsonSerializer.Serialize(longGoals),
            ShortTermGoalsJson = JsonSerializer.Serialize(shortGoals),
            StrategiesJson = JsonSerializer.Serialize(strategies),
            EvaluationJson = JsonSerializer.Serialize(evaluation),
            HomeSchoolJson = JsonSerializer.Serialize(homeSchool),
            LegalBasis = "依据《残疾人教育条例》示范数据。",
            RawJson = JsonSerializer.Serialize(new
            {
                profileSummary = profile, longTermGoals = longGoals, shortTermGoals = shortGoals,
                strategies, evaluation, homeSchool,
                legalBasis = "依据《残疾人教育条例》示范数据。"
            }),
            Status = SpecialEduPlanStatus.Published
        };
        await _iepRepository.InsertAsync(entity);
        added.Add($"IEP（{studentName}）");
        return entity.Id;
    }

    private async Task EnsureResourceAsync(Guid? tenantId, Guid creator, string title,
        SpecialEduCategory category, string modality, Guid? designId, Guid? iepId,
        string[] content, List<string> added)
    {
        var existing = (await _resourceRepository.GetListAsync(x => x.Title == title)).FirstOrDefault();
        if (existing != null) return;
        await _resourceRepository.InsertAsync(new SpecialEduResource(Guid.NewGuid(), tenantId, creator, category, modality)
        {
            Title = title,
            ContentJson = JsonSerializer.Serialize(content),
            RawJson = JsonSerializer.Serialize(new { title, content }),
            TeachingDesignId = designId,
            IepPlanId = iepId,
            Status = SpecialEduPlanStatus.Published
        });
        added.Add($"资源《{title}》");
    }

    /// <summary>
    /// 盲文对照 Mock：现行盲文（原则不标调、分词连写、词间空一方）。
    /// 点位依据《中国盲文》声韵表逐格核验：你=⠝⠊ 好=⠓⠖ 中=⠌⠲ 国=⠛⠢。
    /// </summary>
    private async Task EnsureBrailleMockAsync(Guid? tenantId, Guid creator, Guid courseId, List<string> added)
    {
        const string title = "盲文对照：你好中国（现行盲文示范）";
        var existing = (await _resourceRepository.GetListAsync(
            x => x.Modality == SpecialEduResourceModality.BrailleParallel)).FirstOrDefault();
        if (existing != null) return;
        var pairs = new[]
        {
            new { text = "你好", pinyin = "ni hao", braille = "⠝⠊⠓⠖", note = "你=n(1345⠝)+i(24⠊)；好=h(125⠓)+ao(235⠖)；现行盲文原则不标调" },
            new { text = "中国", pinyin = "zhong guo", braille = "⠌⠲⠛⠢", note = "中=zh(34⠌)+ong(256⠲)；国=g(1245⠛)+o(26⠢，与e同形)" },
            new { text = "你好中国", pinyin = "nihao zhongguo", braille = "⠝⠊⠓⠖⠀⠌⠲⠛⠢", note = "分词连写：你好/中国各成一词，词间空一方（⠀）" },
            new { text = "HELLO", pinyin = "", braille = "⠓⠑⠇⠇⠕", note = "英语一级盲文对照：h(125)+e(15)+l(123)×2+o(135)" },
        };
        var rawJson = JsonSerializer.Serialize(new { title, pairs });
        await _resourceRepository.InsertAsync(new SpecialEduResource(
            Guid.NewGuid(), tenantId, creator, SpecialEduCategory.ShiZhang, SpecialEduResourceModality.BrailleParallel)
        {
            Title = title,
            CourseId = courseId,
            ContentJson = JsonSerializer.Serialize(pairs.Select(p => $"{p.text}｜{p.braille}").ToArray()),
            RawJson = rawJson,
            Status = SpecialEduPlanStatus.Published
        });
        added.Add($"盲文对照《{title}》");
    }
}
