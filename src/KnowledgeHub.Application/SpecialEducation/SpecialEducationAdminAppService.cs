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
        var existing = await _designRepository.CountAsync();
        if (existing > 0)
        {
            return new SeedMockDataResultDto { Message = "当前租户已有特教数据，跳过 seeding。" };
        }

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

        var d1 = new SpecialTeachingDesign(Guid.NewGuid(), tenantId, me, SpecialEduCategory.Peizhi)
        {
            Title = "《认识水果》——培智生活语文（示范）",
            CourseId = mockCourse.Id,
            Subject = "生活语文", Grade = "培智三年级", Duration = 40,
            ObjectivesJson = JsonSerializer.Serialize(new[] { "能指认苹果、香蕉、橙子三种水果", "能在提示下说出水果颜色", "养成洗手后进食的卫生习惯" }),
            KeyPointsJson = JsonSerializer.Serialize(new[] { "水果名称与颜色配对" }),
            DifficultiesJson = JsonSerializer.Serialize(new[] { "从图片泛化到实物" }),
            SectionsJson = JsonSerializer.Serialize(new[] { new { name = "导入", duration = 5, content = "实物展示+问候", activities = new[] { "指认水果" } } }),
            BoardDesignJson = JsonSerializer.Serialize(new[] { "左侧：水果图片区", "右侧：颜色配对区" }),
            StandardBasis = "依据培智学校义务教育课程标准（生活语文）：以生活化、直观化为原则。",
            Status = SpecialEduPlanStatus.Published
        };
        var d2 = new SpecialTeachingDesign(Guid.NewGuid(), tenantId, me, SpecialEduCategory.Guzuzheng)
        {
            Title = "《轮流玩积木》——孤独症社交沟通（示范）",
            Subject = "社交沟通", Grade = "孤独症支持班", Duration = 35,
            ObjectivesJson = JsonSerializer.Serialize(new[] { "能用视觉卡表达“轮到我”", "等待时间达到1分钟", "成功完成2次轮流" }),
            KeyPointsJson = JsonSerializer.Serialize(new[] { "轮流概念与表达" }),
            DifficultiesJson = JsonSerializer.Serialize(new[] { "等待时的情绪调节" }),
            SectionsJson = JsonSerializer.Serialize(new[] { new { name = "结构化练习", duration = 15, content = "视觉日程+计时器", activities = new[] { "轮流搭积木" } } }),
            BoardDesignJson = JsonSerializer.Serialize(new[] { "视觉日程条", "轮流提示卡" }),
            StandardBasis = "依据孤独症教育结构化教学规范：可预测流程+视觉支持。",
            Status = SpecialEduPlanStatus.Published
        };
        await _designRepository.InsertManyAsync(new[] { d1, d2 });

        var studentId = zmq?.Id ?? me;
        var studentName = zmq != null ? (!zmq.Name.IsNullOrEmpty() ? zmq.Name! : "zmq") : "测试学生";
        var iep1 = new IepPlan(Guid.NewGuid(), tenantId, studentId, SpecialEduCategory.TingZhang)
        {
            StudentName = zmq != null ? $"{studentName}（听障示范）" : "测试学生（听障示范）",
            CourseId = mockCourse.Id,
            ProfileJson = JsonSerializer.Serialize(new { summary = "听力损失60dB，视觉学习优势，已选《生活语文·感知与表达》课程。" }),
            LongTermGoalsJson = JsonSerializer.Serialize(new[] { "本学期掌握50个常用书面词汇" }),
            ShortTermGoalsJson = JsonSerializer.Serialize(new[] { "4周内指认20个水果/食物词汇（正确率≥80%）" }),
            StrategiesJson = JsonSerializer.Serialize(new[] { "视觉卡+手语辅助", "小组配对练习" }),
            EvaluationJson = JsonSerializer.Serialize(new[] { "每周词汇指认记录表" }),
            HomeSchoolJson = JsonSerializer.Serialize(new[] { "家长每日15分钟卡片复习", "每周五沟通本反馈" }),
            LegalBasis = "依据《残疾人教育条例》及 IEP 规范：评估—目标—策略—评估闭环，家长参与。",
            Status = SpecialEduPlanStatus.Published
        };
        var iep2 = new IepPlan(Guid.NewGuid(), tenantId, studentId, SpecialEduCategory.ShiZhang)
        {
            StudentName = zmq != null ? $"{studentName}（视障示范）" : "测试学生（视障示范）",
            CourseId = mockCourse.Id,
            ProfileJson = JsonSerializer.Serialize(new { summary = "低视力，听觉触觉优势，已选《生活语文·感知与表达》课程，定向行走需支持。" }),
            LongTermGoalsJson = JsonSerializer.Serialize(new[] { "独立完成校园定向行走" }),
            ShortTermGoalsJson = JsonSerializer.Serialize(new[] { "4周内口述描述教室布局" }),
            StrategiesJson = JsonSerializer.Serialize(new[] { "口述影像+触觉地图" }),
            EvaluationJson = JsonSerializer.Serialize(new[] { "定向行走观察量表" }),
            HomeSchoolJson = JsonSerializer.Serialize(new[] { "家庭触觉标识布置", "每日口述复述" }),
            LegalBasis = "依据《残疾人教育条例》及盲校课程标准相关要求。",
            Status = SpecialEduPlanStatus.Published
        };
        await _iepRepository.InsertManyAsync(new[] { iep1, iep2 });

        var r1 = new SpecialEduResource(Guid.NewGuid(), tenantId, me, SpecialEduCategory.Guzuzheng, SpecialEduResourceModality.SocialStory)
        {
            Title = "社交故事：轮流玩（示范）",
            ContentJson = JsonSerializer.Serialize(new[] { "今天我要和同学一起玩积木。", "轮到我时，我说“轮到我了”。", "等待时我可以数到10。" }),
            TeachingDesignId = d2.Id
        };
        var r2 = new SpecialEduResource(Guid.NewGuid(), tenantId, me, SpecialEduCategory.Peizhi, SpecialEduResourceModality.VisualSupport)
        {
            Title = "视觉支持：洗手步骤卡（示范）",
            ContentJson = JsonSerializer.Serialize(new[] { "1. 开水龙头", "2. 打肥皂", "3. 搓手20秒", "4. 冲洗擦干" }),
            TeachingDesignId = d1.Id
        };
        var r3 = new SpecialEduResource(Guid.NewGuid(), tenantId, me, SpecialEduCategory.TingZhang, SpecialEduResourceModality.VideoScript)
        {
            Title = "视频脚本：水果词汇课（示范）",
            ContentJson = JsonSerializer.Serialize(new[] { "分镜1：特写苹果+字幕+手语框（10s）", "分镜2：学生跟做指认（15s）" }),
            TeachingDesignId = d1.Id
        };
        var r4 = new SpecialEduResource(Guid.NewGuid(), tenantId, me, SpecialEduCategory.ShiZhang, SpecialEduResourceModality.AudioScript)
        {
            Title = "音频脚本：教室定向口述（示范）",
            ContentJson = JsonSerializer.Serialize(new[] { "旁白：从门口出发，向左三步是讲台……（慢速，重复2遍）" }),
            IepPlanId = iep2.Id
        };
        await _resourceRepository.InsertManyAsync(new[] { r1, r2, r3, r4 });

        return new SeedMockDataResultDto
        {
            TeachingDesignCount = 2, IepCount = 2, ResourceCount = 4,
            Message = zmq != null
                ? $"Mock 数据已写入：特教课程《{mockCourse.Title}》、2 教案 / 2 IEP / 4 资源，学生 zmq({studentName})已选课。"
                : "Mock 数据已写入：1 特教课程、2 教案 / 2 IEP / 4 资源；未找到 zmq 用户，IEP 挂占位学生。"
        };
    }
}
