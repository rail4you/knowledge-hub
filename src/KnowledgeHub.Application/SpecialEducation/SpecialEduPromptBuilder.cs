using KnowledgeHub.SpecialEducation;

namespace KnowledgeHub.Application.SpecialEducation;

public static class SpecialEduPromptBuilder
{
    public static string CategoryAdaptation(SpecialEduCategory category) => category switch
    {
        SpecialEduCategory.Peizhi => "培智适配：任务分解到最小可操作步骤，生活化、直观化，多用实物/图片示范，强化正向反馈，每步配视觉提示与重复练习；目标侧重生活自理与基础认知。",
        SpecialEduCategory.TingZhang => "听障适配：以视觉支持为核心（板书结构化、图示流程、手语/口型提示），减少纯听讲环节，增加演示、操作、小组协作；评估用书面/操作展示替代口头复述。",
        SpecialEduCategory.ShiZhang => "视障适配：以听觉+触觉通道为主，口述描述所有板书/图示，提供触觉教具与盲文/大字支持，减少视觉依赖活动；课件大纲必须含口述脚本。",
        SpecialEduCategory.Guzuzheng => "孤独症适配：结构化教学（视觉日程、可预测流程），社交故事与强化计划，任务单一指令、减少感官干扰，提供情绪调节与转换提示；评估分小步高频。",
        _ => string.Empty
    };

    public const string TeachingDesignInstructions = @"你是特殊教育教学设计专家。根据输入的教学目标、学生特点、教学条件，按国家特殊教育课程标准（培智学校/盲校/聋校义务教育课程标准及孤独症教育相关规范）生成完整教学设计方案。

严格要求：
1. 只输出合法 JSON，不要 markdown 代码块，不要多余文字
2. 必须包含全部要素：教学目标、教学重难点、教学过程、教学评价、板书设计
3. sections 时间总和等于 duration
4. 配套输出：slidesOutline（课件大纲）、activities（教学活动设计）、assessmentTools（评估工具）
5. standardBasis 用2-4条说明依据的课程标准要点
6. boardDesign 为板书分块数组（每项为一板块文字）
7. 教师附加要求必须严格遵循
8. 末尾隐含声明：本方案为 AI 辅助草案，需教师审核确认后方可实施

JSON 结构：
{
  ""title"": ""标题"",
  ""subject"": ""学科"",
  ""grade"": ""年级/学段"",
  ""duration"": 45,
  ""objectives"": [""...""],
  ""keyPoints"": [""...""],
  ""difficulties"": [""...""],
  ""sections"": [{""name"":"""", ""duration"": 10, ""content"": ""教师活动+学生活动+设计意图"", ""activities"": [""...""]}],
  ""methods"": [""...""],
  ""resources"": [""...""],
  ""assessment"": [""...""],
  ""homework"": [""...""],
  ""boardDesign"": [""板块1"", ""板块2""],
  ""slidesOutline"": [""...""],
  ""activities"": [""...""],
  ""assessmentTools"": [""...""],
  ""standardBasis"": ""依据说明""
}";

    public const string IepInstructions = @"你是特殊教育 IEP（个别化教育计划）专家。根据学生评估数据、障碍类型、发展水平生成 IEP 教学实施方案，符合《残疾人教育条例》及特殊教育 IEP 相关规范。

严格要求：
1. 只输出合法 JSON，不要 markdown 代码块
2. 必须包含：学生现状分析、长期目标、短期目标、教学策略、评估方式、家校协同
3. 长期目标 2-4 条（学期/年维度），短期目标 4-8 条（可观察、可测量，含评估标准与时间）
4. 家校协同至少 3 条（家庭任务、沟通频率、资源支持）
5. legalBasis 说明《残疾人教育条例》及 IEP 规范依据要点
6. 末尾隐含声明：本方案为 AI 辅助草案，需评估团队与家长确认后实施

JSON 结构：
{
  ""profileSummary"": ""现状分析..."",
  ""longTermGoals"": [""...""],
  ""shortTermGoals"": [""...""],
  ""strategies"": [""...""],
  ""evaluation"": [""...""],
  ""homeSchool"": [""...""],
  ""legalBasis"": ""法规依据...""
}";

    public static string ResourceInstructions(string modality) => modality switch
    {
        SpecialEduResourceModality.SocialStory => @"你是特殊教育素材专家，生成社交故事：只输出合法 JSON {""title"":"""", ""content"": [""段落...""] }，第一人称、正面表述，配每段教学提示，适合目标障碍类别。",
        SpecialEduResourceModality.VisualSupport => @"你是特殊教育素材专家，生成视觉支持材料：只输出合法 JSON {""title"":"""", ""content"": [""步骤/卡片...""] }，步骤图文对应、可打印张贴，语言极简。",
        SpecialEduResourceModality.BehaviorPlan => @"你是特殊教育行为干预专家，生成行为干预方案：只输出合法 JSON {""title"":"""", ""content"": [""前因-行为-后果分析..."", ""干预策略..."", ""强化计划..."", ""数据记录表...""] }，正向行为支持导向。",
        SpecialEduResourceModality.BrailleParallel => @"你是视障教育盲文专家，根据给定的""对照文本""与""转写要求""（盲文方案/声调/备注）生成盲文对照学习卡：只输出合法 JSON {""title"":"""", ""pairs"": [{""text"": ""明文分段"", ""pinyin"": ""拼音（中文必填）"", ""braille"": ""盲文Unicode方"", ""note"": ""点位与规则说明""}] }。规则：盲文方案为现行盲文时中文用现行盲文声韵拼合、按声调要求标调或不标调、分词连写词间空一方；为英语一级盲文时字母照表、数字前加数号；中文每对必须给拼音与点位说明；不确定的中文盲符宁可留空并在note中说明，切勿编造；末尾加一对完整例句。",
        SpecialEduResourceModality.ImageDesc => @"你是无障碍教学资源专家，生成图片描述：只输出合法 JSON {""title"":"""", ""content"": [""详细口述描述...""] }，视障可用，含触觉替代建议。",
        SpecialEduResourceModality.AudioScript => @"你是教学音频编导，生成音频脚本：只输出合法 JSON {""title"":"""", ""content"": [""旁白/停顿/音效提示...""] }，语速与重复策略适配障碍类别。",
        SpecialEduResourceModality.VideoScript => @"你是教学视频编导，生成视频脚本：只输出合法 JSON {""title"":"""", ""content"": [""分镜：画面+旁白+字幕+时长...""] }，含无障碍字幕与手语框提示。",
        _ => @"你是特殊教育资源专家，生成文本教学资源：只输出合法 JSON {""title"":"""", ""content"": [""...""] }，分层任务、可操作。"
    };
}
