using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.AI;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub;

public class KnowledgeHubDataSeederContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IRepository<TeachingScene, Guid> _sceneRepository;

    public KnowledgeHubDataSeederContributor(IRepository<TeachingScene, Guid> sceneRepository)
    {
        _sceneRepository = sceneRepository;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        // 系统内置教学场景模板是全局数据（TenantId 为空），只在宿主侧播种一次，避免各租户重复插入。
        if (context.TenantId == null)
        {
            await SeedTeachingScenesAsync();
        }
    }

    private async Task SeedTeachingScenesAsync()
    {
        if (await _sceneRepository.AnyAsync(x => x.IsSystem))
        {
            return;
        }

        var defaults = new List<(TeachingSceneCategory Category, string Name, string Prompt)>
        {
            // ── 图片生成场景 ──
            (TeachingSceneCategory.Image, "课堂讲解",
                "明亮的现代化教室里，一位亲切的年轻教师站在讲台前，身后黑板写着数学公式与几何图形，学生课桌上摆着课本，卡通插画风格，暖色调，适合小学课堂教学配图"),
            (TeachingSceneCategory.Image, "科学实验",
                "科学实验室里，几名中学生围在实验台旁观察试管中冒出的彩色气泡，仪器整洁，明亮照明，写实教育插画风格，适合科学课配图"),
            (TeachingSceneCategory.Image, "校园生活",
                "阳光明媚的校园操场，孩子们在绿茵场上奔跑运动，周围绿树鲜花，蓝天白云，温馨童趣插画风格，适合学生手抄报配图"),
            (TeachingSceneCategory.Image, "古诗意境",
                "中国传统水墨山水画风格，江南水乡小桥流水、垂柳人家，烟雨朦胧，淡彩晕染，适合语文课古诗词讲解配图"),
            (TeachingSceneCategory.Image, "物理电路",
                "清晰的教学示意图，简单串联电路包含电池、灯泡、开关与导线，箭头标注电流方向，白色背景，简洁扁平教育插画"),
            (TeachingSceneCategory.Image, "历史课堂",
                "中国古代私塾课堂场景，先生身着长衫授课，书童研墨，背景挂有山水字画，工笔重彩风格，适合历史教学配图"),

            // ── 视频首帧场景 ──
            (TeachingSceneCategory.VideoScene, "课堂讲解",
                "明亮的教室里，一位年轻教师站在讲台前，身后黑板写着数学公式，正微笑着讲解，卡通插画风格，暖色调"),
            (TeachingSceneCategory.VideoScene, "科学实验",
                "科学实验室里，学生正在观察试管中冒出的彩色泡泡，实验台整洁，明亮光线，写实教育插画风格"),
            (TeachingSceneCategory.VideoScene, "古诗意境",
                "中国传统水墨画风格，江南水乡小桥流水、垂柳人家，烟雨朦胧，淡彩晕染，适合语文课配图"),
            (TeachingSceneCategory.VideoScene, "校园生活",
                "阳光明媚的校园操场，孩子们在绿茵场上运动，蓝天白云，温馨童趣插画风格"),

            // ── 视频运镜 / 动作 ──
            (TeachingSceneCategory.VideoMotion, "缓慢推进", "镜头缓慢向前推进，人物微笑点头，画面稳定柔和"),
            (TeachingSceneCategory.VideoMotion, "左右摇移", "镜头从左向右缓慢平移，缓缓展示整个场景"),
            (TeachingSceneCategory.VideoMotion, "轻推特写", "镜头轻微推进放大，人物抬手示意，动作自然流畅"),
            (TeachingSceneCategory.VideoMotion, "静中有动", "主体保持稳定，背景的窗帘与树叶轻轻飘动，微风感"),
        };

        var order = 0;
        foreach (var (category, name, prompt) in defaults)
        {
            var scene = new TeachingScene(Guid.NewGuid(), name, prompt, category)
            {
                TenantId = null,
                IsSystem = true,
                SortOrder = order++,
            };
            await _sceneRepository.InsertAsync(scene, autoSave: true);
        }
    }
}
