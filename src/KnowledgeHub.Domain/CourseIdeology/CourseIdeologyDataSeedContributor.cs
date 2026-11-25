using System;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.CourseIdeology;
using KnowledgeHub.Features;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Features;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.CourseIdeology;

/// <summary>
/// 课程思政模块的种子数据：给**还没有任何思政元素**的租户播种一套通用元素
/// （对照《高等学校课程思政建设指导纲要》的常见育人要点，学校可自行增删改）。
///
/// <para>幂等：只在租户零元素时播种，不会覆盖学校自己维护的体系；
/// Feature <c>KnowledgeHub.CourseIdeology</c> 关闭时不播种（模块没装就不落数据）。</para>
/// </summary>
public class CourseIdeologyDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private static readonly (string Name, string Category, string Description)[] DefaultElements =
    {
        ("科技自立自强", "理想信念", "关键核心技术攻关、国产化替代、基础研究突破等，引导学生理解专业与国家竞争力的关系。"),
        ("家国情怀", "理想信念", "国家重大工程、区域发展、乡村振兴、民族团结等，把个人成长融入国家发展。"),
        ("法治意识", "价值取向", "法律法规原文与合规案例（网络安全法、个人信息保护法等），树立依法办事、合规操作的底线思维。"),
        ("职业道德", "职业素养", "行业规范、职业操守、保密与责任担当，训练学生进入岗位后的行为准则。"),
        ("总体国家安全观", "价值取向", "政治、网络、数据、生物、供应链等安全维度，理解发展与安全的辩证关系。"),
        ("工匠精神", "职业素养", "精益求精、长期攻关、标准与规范，体会一线工程师与技术能手的职业品格。"),
    };

    private readonly IRepository<IdeologyElement, Guid> _elementRepository;
    private readonly IFeatureChecker _featureChecker;
    private readonly ILogger<CourseIdeologyDataSeedContributor> _logger;

    public CourseIdeologyDataSeedContributor(
        IRepository<IdeologyElement, Guid> elementRepository,
        IFeatureChecker featureChecker,
        ILogger<CourseIdeologyDataSeedContributor> logger)
    {
        _elementRepository = elementRepository;
        _featureChecker = featureChecker;
        _logger = logger;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (!await _featureChecker.IsEnabledAsync(KnowledgeHubFeatures.CourseIdeology))
        {
            return;
        }

        var existing = await _elementRepository.GetListAsync();
        if (existing.Count > 0)
        {
            return;
        }

        var order = 1;
        foreach (var (name, category, description) in DefaultElements)
        {
            await _elementRepository.InsertAsync(new IdeologyElement(Guid.NewGuid(), name, context.TenantId)
            {
                Category = category,
                Description = description,
                SortOrder = order++,
                IsEnabled = true,
            }, autoSave: true);
        }

        _logger.LogInformation("[CourseIdeology] 已为租户 {TenantId} 播种 {Count} 个默认思政元素",
            context.TenantId, DefaultElements.Length);
    }
}
