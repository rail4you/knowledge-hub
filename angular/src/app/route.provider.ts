import { RoutesService, eLayoutType } from '@abp/ng.core';
import { inject, provideAppInitializer } from '@angular/core';

export const APP_ROUTE_PROVIDER = [
  provideAppInitializer(() => {
    configureRoutes();
  }),
];

function configureRoutes() {
  const routes = inject(RoutesService);
  routes.add([
    // ==========================================================
    //  首页 / 系统工作台
    // ==========================================================
    {
      path: '/',
      name: '::Menu:Home',
      iconClass: 'fas fa-home',
      order: 1,
      layout: eLayoutType.empty,
      // 门户首页：不再出现在管理端侧边栏（管理端首页改为「系统工作台」）。
      invisible: true,
    },
    {
      path: '/admin/workbench',
      name: '::Menu:Workbench',
      iconClass: 'fas fa-gauge-high',
      order: 1,
      layout: eLayoutType.application,
    },
    // ══════════════════════════════════════════════════════════
    //  ① 资源管理
    // ══════════════════════════════════════════════════════════
    {
      path: '/resource-management',
      name: '::Menu:ResourceManagement',
      iconClass: 'fas fa-folder-open',
      order: 2,
      layout: eLayoutType.application,
      // 分组要求 Resources 权限：联盟审核员（Resources 权限已授予）能看到此分组。
      // 组内其它菜单（我的收藏/搜索/搜索历史）要求 Search 权限，联盟审核员无此权限，故只显示"资源"（审核）。
      requiredPolicy: 'KnowledgeHub.Resources',
    },
    {
      path: '/resources',
      name: '::Menu:Resources',
      iconClass: 'fas fa-folder-open',
      parentName: '::Menu:ResourceManagement',
      order: 1,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Resources',
    },
    {
      path: '/search',
      name: '::Menu:DocumentSearch',
      iconClass: 'fas fa-search',
      parentName: '::Menu:ResourceManagement',
      order: 2,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Search',
    },
    {
      path: '/my/search-history',
      name: '::Menu:SearchHistory',
      iconClass: 'fas fa-history',
      parentName: '::Menu:ResourceManagement',
      order: 3,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Search',
    },

    // ══════════════════════════════════════════════════════════
    //  ② AI 管理
    // ══════════════════════════════════════════════════════════
    {
      path: '/ai-management',
      name: '::Menu:AIManagement',
      iconClass: 'fas fa-robot',
      order: 3,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.AI',
    },
    {
      path: '/ai/chat',
      name: '::Menu:AIChat',
      iconClass: 'fas fa-comment-dots',
      parentName: '::Menu:AIManagement',
      order: 1,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.AI',
    },
    {
      path: '/teaching/agents',
      name: '::Menu:TeachingAgents',
      iconClass: 'fas fa-microchip',
      parentName: '::Menu:AIManagement',
      order: 2,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.TeachingAgents.Manage',
    },
    {
      path: '/teaching/agent-tasks',
      name: '::Menu:TeachingAgentTasks',
      iconClass: 'fas fa-share-nodes',
      parentName: '::Menu:AIManagement',
      order: 3,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.TeachingAgents.Assign',
    },
    // AI 子功能（职业规划）留在此组。
    // 教案生成、案例分析（课程案例库）、习题生成已整合到「专业和课程管理」下的
    // 教案管理 / 案例库 / 习题管理；教学图片 / 短视频生成已整合到「课程资源」，
    // 生成结果作为课程资源走审核流程。此组不再单独暴露这些入口；
    // /ai/lesson-plan、/ai/case-analysis、/ai/exercise-generate、
    // /ai/image-generation、/ai/video-generation 路由保留，
    // 用于 AI 任务通知、任务中心等深度链接与结果回看。
    {
      path: '/ai/career-guidance',
      name: '::Menu:CareerGuidance',
      iconClass: 'fas fa-compass',
      parentName: '::Menu:AIManagement',
      order: 6,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.AI.CareerGuidance',
    },
    {
      path: '/ai/model-management',
      name: '::Menu:ModelManagement',
      iconClass: 'fas fa-cog',
      parentName: '::Menu:AIManagement',
      order: 10,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.AI.LessonPlan',
    },
    {
      path: '/ai/tasks',
      name: '::Menu:AITaskCenter',
      iconClass: 'fas fa-list-check',
      parentName: '::Menu:AIManagement',
      order: 11,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.AI',
    },

    // ══════════════════════════════════════════════════════════
    //  ③ 专业和课程管理
    // ══════════════════════════════════════════════════════════
    {
      path: '/course-management',
      name: '::Menu:CourseManagement',
      iconClass: 'fas fa-graduation-cap',
      order: 4,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Courses',
    },
    {
      path: '/admin/majors',
      name: '::Menu:MajorManagement',
      iconClass: 'fas fa-graduation-cap',
      parentName: '::Menu:CourseManagement',
      order: 2,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Majors.Create',
    },
    {
      path: '/admin/micro-majors',
      name: '::Menu:MicroMajorManagement',
      iconClass: 'fas fa-cubes',
      parentName: '::Menu:CourseManagement',
      order: 3,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.MicroMajors.Create',
    },
    {
      path: '/learning/course-list',
      name: '::Menu:CourseList',
      iconClass: 'fas fa-book-open',
      parentName: '::Menu:CourseManagement',
      order: 4,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Courses',
    },
    {
      path: '/learning/student-enrollment',
      name: '::Menu:StudentEnrollment',
      iconClass: 'fas fa-user-plus',
      parentName: '::Menu:CourseManagement',
      order: 5,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Courses.ManageEnrollment',
    },
    {
      path: '/learning/chapter-management',
      name: '::Menu:ChapterManagement',
      iconClass: 'fas fa-sitemap',
      parentName: '::Menu:CourseManagement',
      order: 6,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Courses.Edit',
    },
    {
      path: '/learning/exercise-management',
      name: '::Menu:ExerciseManagement',
      iconClass: 'fas fa-tasks',
      parentName: '::Menu:CourseManagement',
      order: 7,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Courses.Edit',
    },
    {
      path: '/learning/chapter-exercise',
      name: '::Menu:ChapterExercise',
      iconClass: 'fas fa-list-check',
      parentName: '::Menu:CourseManagement',
      order: 8,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Courses.Edit',
    },
    {
      path: '/learning/course-resource',
      name: '::Menu:CourseResource',
      iconClass: 'fas fa-folder-open',
      parentName: '::Menu:CourseManagement',
      order: 9,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Courses.Edit',
    },
    {
      path: '/learning/lesson-plan',
      name: '::Menu:LessonPlanManagement',
      iconClass: 'fas fa-file-signature',
      parentName: '::Menu:CourseManagement',
      order: 9.5,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Courses.Edit',
    },
    {
      path: '/learning/exam-management',
      name: '::Menu:ExamManagement',
      iconClass: 'fas fa-file-circle-check',
      parentName: '::Menu:CourseManagement',
      order: 9.7,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Courses.Edit',
    },
    {
      path: '/learning/course-case',
      name: '::Menu:CourseCaseLibrary',
      iconClass: 'fas fa-briefcase',
      parentName: '::Menu:CourseManagement',
      order: 9.6,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Courses.Edit',
    },
    {
      path: '/learning/chapter-resource',
      name: '::Menu:ChapterResource',
      iconClass: 'fas fa-file-lines',
      parentName: '::Menu:CourseManagement',
      order: 10,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Courses.Edit',
    },
    {
      path: '/learning/knowledge-graph/:courseId',
      name: '::Menu:KnowledgeGraph',
      parentName: '::Menu:CourseManagement',
      layout: eLayoutType.application,
      
      invisible: true,
    },
    {
      path: '/learning/learning-statistics',
      name: '::Menu:LearningStatistics',
      iconClass: 'fas fa-chart-bar',
      parentName: '::Menu:CourseManagement',
      order: 11,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Learning.ViewStatistics',
    },
    // ══════════════════════════════════════════════════════════
    //  ⑤ 资源库管理：教学资源库管理（建库 / 共建 / 分配教师）
    //     ＋ 学校管理（与租户 1:1 的学校档案：名称 / 徽标 / 简介 / 域名绑定）
    // ══════════════════════════════════════════════════════════
    {
      // 资源库管理栏目：紧跟「系统工作台」(order 1) 之下（order 1.5），
      // 「资源管理」是 2，1.5 恰好落在两者之间，无需整体重排其它栏目。
      path: '/resource-library-management',
      name: '::Menu:ResourceLibraryManagement',
      iconClass: 'fas fa-book-bookmark',
      order: 1.5,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.ResourceLibraries',
    },
    {
      // 教学资源库工作台（教师端）：资源库汇总 + 快捷访问 + 库内建设操作。
      // 原为顶层独立菜单项，现并入「资源库管理」栏目，作为该栏目的第一个子项。
      path: '/teaching-resource-libraries',
      name: '::Menu:WorkbenchTeachingLibrary',
      iconClass: 'fas fa-layer-group',
      parentName: '::Menu:ResourceLibraryManagement',
      order: 1,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.ResourceLibraries',
    },
    {
      // 教学资源库管理（管理端）：建库 / 删库 / 设置私有·公开·院校共建 / 分配教师。
      // 策略用 Create（而非 Default）：企业用户有 Default/Build 但不应看到建库管理页。
      path: '/admin/libraries',
      name: '::Menu:ResourceLibraries',
      iconClass: 'fas fa-layer-group',
      parentName: '::Menu:ResourceLibraryManagement',
      order: 2,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.ResourceLibraries.Create',
    },
    {
      // 产教合作（V2.0 P5）：需求大厅 + 项目台账。
      path: '/admin/cooperation',
      name: '::Menu:Cooperation',
      iconClass: 'fas fa-handshake',
      parentName: '::Menu:ResourceLibraryManagement',
      order: 3.2,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Cooperation',
    },
    {
      // 企业工作台（V2.0 P5）：企业用户专属入口。
      path: '/enterprise/workbench',
      name: '::Menu:EnterpriseWorkbench',
      iconClass: 'fas fa-building',
      order: 1.8,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Cooperation',
    },
    {
      // 我的跨校引用（V2.0 P4）：引用共建库资源到本校课程。
      path: '/teaching/shared-refs',
      name: '::Menu:SharedRefs',
      iconClass: 'fas fa-share-nodes',
      parentName: '::Menu:ResourceLibraryManagement',
      order: 2.7,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Courses.Edit',
    },
    {
      // 我的共建任务（教师端）：协作校教师提交交付物。
      path: '/teaching/build-tasks',
      name: '::Menu:MyBuildTasks',
      iconClass: 'fas fa-clipboard-list',
      parentName: '::Menu:ResourceLibraryManagement',
      order: 2.2,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.ResourceLibraries',
    },
    {
      // 共建任务（V2.0 P3）：建任务 / 分派协作校 / 交付验收 / 任务模板。
      path: '/admin/libraries/tasks',
      name: '::Menu:BuildTasks',
      iconClass: 'fas fa-list-check',
      parentName: '::Menu:ResourceLibraryManagement',
      order: 2.5,
      layout: eLayoutType.application,
      // 用 ManageTask：企业用户只有 Default/Build/SubmitTask，不应看到「建任务/验收」入口
      requiredPolicy: 'KnowledgeHub.ResourceLibraries.ManageTask',
    },
    {
      // 审核中心（V2.0）：院校初审 + 联盟终审。菜单对两类审核人都可见，
      // 页面内部再按 Audit / LeagueAudit 权限控制可操作范围（避免菜单出现两套）。
      path: '/admin/libraries/audits',
      name: '::Menu:ReviewCenter',
      iconClass: 'fas fa-clipboard-check',
      parentName: '::Menu:ResourceLibraryManagement',
      order: 3,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.ResourceLibraries',
    },
    {
      // 学校有效期（V2.0 P5）：平台管理员配置各校账号有效期。
      path: '/admin/school-validity',
      name: '::Menu:SchoolValidity',
      iconClass: 'fas fa-calendar-check',
      parentName: '::Menu:SearchAndTenantManagement',
      order: 7,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Schools.ManageValidity',
    },
    {
      // 学校管理：学校档案（名称 / 徽标 / 简介 / 域名绑定），配置内容即 /school/{tenantId}
      // 院校门户展示的内容。host 全局管理员可管理所有学校，租户管理员只管理本校。
      path: '/admin/school',
      name: '::Menu:Schools',
      iconClass: 'fas fa-school-flag',
      parentName: '::Menu:ResourceLibraryManagement',
      order: 3,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Schools',
    },
    {
      // 「其他学员」目录（PRD 5.3）：系统级默认目录，学员首次学习时自动建档，
      // 不归属任何班级，与班级数据天然隔离；支持按学员维度查看、导出。
      path: '/admin/other-learners',
      name: '::Menu:OtherLearners',
      iconClass: 'fas fa-user-secret',
      parentName: '::Menu:ResourceLibraryManagement',
      order: 4,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.ResourceLibraries.ManageLearners',
    },
    {
      // 课程证书中心：审核发放学员课程证书。
      //   · 平台管理员 → 全部院校 + 证书模板配置（模板由平台统一定义，院校/老师不可自定义）；
      //   · 库所有者（资源库归属租户的租户管理员）→ 审核本机构主建库的申请并发证。
      path: '/admin/certificates',
      name: '::Menu:Certificates',
      iconClass: 'fas fa-certificate',
      parentName: '::Menu:ResourceLibraryManagement',
      order: 5,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Certificates.Review',
    },

    // ══════════════════════════════════════════════════════════
    //  ⑥ 智学分析（独立栏目）：从「专业和课程管理」移出
    //     智学分析（纸质试卷 → 薄弱点 → 教学诊断 → 课程联动）
    //     智能知识点分析（灰区确认 / 合并 / 拆分 / 习题关联）
    // ══════════════════════════════════════════════════════════
    {
      path: '/learning-analysis',
      name: '::Menu:LearningAnalysisGroup',
      iconClass: 'fas fa-chart-line',
      order: 6,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Courses',
    },
    {
      // 智学分析 · 试卷分析：上传→OCR→校对→推送
      path: '/learning/analysis/paper',
      name: '::Menu:LearningAnalysisPaper',
      iconClass: 'fas fa-file-signature',
      parentName: '::Menu:LearningAnalysisGroup',
      order: 1,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Courses',
    },
    {
      // 智学分析 · 分析报告：班级薄弱点 / 教学诊断 / 导出
      path: '/learning/analysis/report',
      name: '::Menu:LearningAnalysisReport',
      iconClass: 'fas fa-chart-pie',
      parentName: '::Menu:LearningAnalysisGroup',
      order: 2,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Courses',
    },
    {
      // 智学分析 · 课程联动设置
      path: '/learning/analysis/linkage',
      name: '::Menu:LearningAnalysisLinkage',
      iconClass: 'fas fa-diagram-project',
      parentName: '::Menu:LearningAnalysisGroup',
      order: 3,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Courses',
    },
    {
      // 智能知识点分析：灰区确认 / 合并 / 拆分 / 习题关联
      path: '/learning/knowledge-points',
      name: '::Menu:KnowledgePointManagement',
      iconClass: 'fas fa-diagram-project',
      parentName: '::Menu:LearningAnalysisGroup',
      order: 4,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Courses',
    },

    // ══════════════════════════════════════════════════════════
    //  ⑦ 实训和就业管理
    // ══════════════════════════════════════════════════════════
    {
      path: '/training-management',
      name: '::Menu:TrainingManagement',
      iconClass: 'fas fa-briefcase',
      order: 7,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Employment',
    },
    // ══════════════════════════════════════════════════════════
    //  实训（独立栏目）：实训管理 / 实训任务 / 仿真实训 / 智能体聊天
    // ══════════════════════════════════════════════════════════
    {
      path: '/practicum',
      name: '::Menu:PracticumGroup',
      iconClass: 'fas fa-chalkboard-teacher',
      order: 8,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Practicum',
    },
    {
      path: '/admin/practicum/projects',
      name: '::Menu:PracticumManagement',
      iconClass: 'fas fa-tasks',
      parentName: '::Menu:PracticumGroup',
      order: 1,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Practicum.Create',
    },
    {
      path: '/admin/practicum/tasks',
      name: '::Menu:PracticumTasks',
      iconClass: 'fas fa-list-check',
      parentName: '::Menu:PracticumGroup',
      order: 2,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Practicum',
    },
    {
      path: '/admin/practicum/simulations',
      name: '::Menu:PracticumSimulation',
      iconClass: 'fas fa-cubes',
      parentName: '::Menu:PracticumGroup',
      order: 3,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Practicum',
    },
    {
      path: '/admin/practicum/chat',
      name: '::Menu:PracticumAgentChat',
      iconClass: 'fas fa-comments',
      parentName: '::Menu:PracticumGroup',
      order: 4,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Practicum',
    },
    {
      path: '/admin/employment/jobs',
      name: '::Menu:EmploymentJobManagement',
      iconClass: 'fas fa-clipboard-list',
      parentName: '::Menu:TrainingManagement',
      order: 6,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Employment.PublishJob',
    },
    {
      path: '/admin/employment/interviews',
      name: '::Menu:EmploymentInterviewManagement',
      iconClass: 'fas fa-calendar-check',
      parentName: '::Menu:TrainingManagement',
      order: 7,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Employment.ScheduleInterview',
    },
    {
      path: '/admin/employment/statistics',
      name: '::Menu:EmploymentStatistics',
      iconClass: 'fas fa-chart-column',
      parentName: '::Menu:TrainingManagement',
      order: 8,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Employment.ViewStatistics',
    },
    {
      path: '/admin/employment/outcomes',
      name: '::Menu:EmploymentOutcomeManagement',
      iconClass: 'fas fa-briefcase',
      parentName: '::Menu:TrainingManagement',
      order: 9,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Employment.ManageOutcome',
    },
    {
      // 招聘直播（就业管理）：面向候选学生的直播面试/宣讲
      path: '/admin/recruitment-live',
      name: '::Menu:RecruitmentLive',
      iconClass: 'fas fa-video',
      parentName: '::Menu:TrainingManagement',
      order: 10,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.RecruitmentLive.Create',
    },
    {
      // 课程直播（专业和课程管理）：复用招聘直播能力，创建时必须关联已有课程
      path: '/admin/course-live',
      name: '::Menu:CourseLive',
      iconClass: 'fas fa-video',
      parentName: '::Menu:CourseManagement',
      order: 12,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.RecruitmentLive.Create',
    },

    // ══════════════════════════════════════════════════════════
    //  ⑧ 资讯和双高信息管理
    // ══════════════════════════════════════════════════════════
    {
      path: '/info-management',
      name: '::Menu:InfoManagement',
      iconClass: 'fas fa-newspaper',
      order: 9,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.News',
    },
    {
      path: '/news',
      name: '::Menu:News',
      iconClass: 'fas fa-newspaper',
      parentName: '::Menu:InfoManagement',
      order: 1,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.News',
    },
    {
      path: '/admin/news',
      name: '::Menu:NewsManagement',
      iconClass: 'fas fa-bullhorn',
      parentName: '::Menu:InfoManagement',
      order: 2,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.News.Create',
    },
    {
      path: '/admin/assessment/double-high',
      name: '::Menu:DoubleHighManagement',
      iconClass: 'fas fa-sliders-h',
      parentName: '::Menu:InfoManagement',
      order: 3,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.DoubleHigh.ManageProject',
    },
    {
      path: '/assessment/double-high/projects',
      name: '::Menu:DoubleHighProjects',
      iconClass: 'fas fa-layer-group',
      parentName: '::Menu:InfoManagement',
      order: 4,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.DoubleHigh',
    },
    {
      path: '/assessment/double-high/project',
      name: '::Menu:DoubleHighProject',
      parentName: '::Menu:InfoManagement',
      layout: eLayoutType.application,
      invisible: true,
    },

    // ══════════════════════════════════════════════════════════
    //  ⑩ 平台运维（::Menu:SearchAndTenantManagement）
    //  资源搜索（搜索引擎管理 / 检索统计）与资源处理进度。
    //  教学资源库管理、学校管理已移入「资源库管理」栏目。
    // ══════════════════════════════════════════════════════════
    {
      path: '/admin-search-tenant',
      name: '::Menu:SearchAndTenantManagement',
      iconClass: 'fas fa-search',
      order: 10,
      layout: eLayoutType.application,
      // 平台运维分组：用 ManageIndex（学校管理员/平台管理员）而不是 Search.Default，
      // 否则企业用户（有 Search.Default）会看到一个空的「平台运维」分组。
      requiredPolicy: 'KnowledgeHub.Search.ManageIndex',
    },
    {
      path: '/admin/meilisearch',
      name: '::Menu:MeiliSearchDashboard',
      iconClass: 'fas fa-tachometer-alt',
      parentName: '::Menu:SearchAndTenantManagement',
      order: 1,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Search.ManageIndex',
    },
    {
      path: '/admin/search-statistics',
      name: '::Menu:SearchStatistics',
      iconClass: 'fas fa-chart-bar',
      parentName: '::Menu:SearchAndTenantManagement',
      order: 2,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Search.ManageIndex',
    },
    {
      // 资源进度：上传 → 预览 → 院校审核 → 索引 → 联盟审核 → 学生可见
      path: '/admin/resource-progress',
      name: '::Menu:ResourceProgress',
      iconClass: 'fas fa-diagram-project',
      parentName: '::Menu:SearchAndTenantManagement',
      order: 3,
      layout: eLayoutType.application,
      // 运维视图：用统计权限，避免企业/教师进入运营后台
      requiredPolicy: 'KnowledgeHub.Resources.ViewStatistics',
    },
    {
      // 资源任务：媒体处理 + 文档索引 + 视频索引，按资源聚合
      path: '/admin/resource-tasks',
      name: '::Menu:ResourceTasks',
      iconClass: 'fas fa-tasks',
      parentName: '::Menu:SearchAndTenantManagement',
      order: 4,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Resources.ViewStatistics',
    },
    // ══════════════════════════════════════════════════════════
    //  多校协同管理（全局管理员专属）
    //  ⚠ 入口权限必须用 KnowledgeHub.AccountValidity（仅 host admin 被授予），
    //  不能复用 KnowledgeHub.Users —— 租户 SchoolAdmin 也持有该权限（租户用户管理），
    //  且 GrantAllPoliciesMiddleware 会把它注入给所有管理角色，导致租户管理员看到此菜单。
    // ══════════════════════════════════════════════════════════
    {
      path: '/multi-school-admin',
      name: '::Menu:MultiSchool',
      iconClass: 'fas fa-school',
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.AccountValidity',
    },
    {
      path: '/admin/account-validity',
      name: '::Menu:AccountValidity',
      iconClass: 'fas fa-hourglass-half',
      parentName: '::Menu:MultiSchool',
      order: 10,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.AccountValidity',
    },
    {
      // 品牌设置：与多校协同同属全局管理员专属区，独立权限 KnowledgeHub.Branding。
      path: '/admin/branding',
      name: '::Menu:Branding',
      iconClass: 'fas fa-palette',
      parentName: '::Menu:MultiSchool',
      order: 20,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Branding',
    },
    {
      // 存储容量监控：每租户文件容量与配额（跳租户统计），与品牌设置同属全局管理员专属区。
      path: '/admin/storage-monitor',
      name: '::Menu:StorageMonitor',
      iconClass: 'fas fa-database',
      parentName: '::Menu:MultiSchool',
      order: 30,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.StorageMonitor',
    },
    {
      // SMTP 邮件设置：163/QQ 预设 + 测试发送，独立权限 KnowledgeHub.Smtp。
      // 挂在 ABP 内置「管理」分组（Administration）下；不挂 MultiSchool——
      // 其父菜单要求 host-only 的 AccountValidity，而租户 SchoolAdmin 也要配本租户 SMTP。
      path: '/admin/smtp-settings',
      name: '::Menu:SmtpSettings',
      iconClass: 'fas fa-envelope-open-text',
      parentName: 'AbpUiNavigation::Menu:Administration',
      order: 50,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Smtp',
    },
    {
      // 对象存储设置（阿里云 OSS）：与 SMTP 同模式——租户 SchoolAdmin 配本租户 Bucket/AK，
      // host admin 写全局回退；挂在 ABP 内置「管理」分组下。
      path: '/admin/oss-settings',
      name: '::Menu:OssSettings',
      iconClass: 'fas fa-cloud',
      parentName: 'AbpUiNavigation::Menu:Administration',
      order: 51,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Oss',
    },
    // ══════════════════════════════════════════════════════════
    //  隐藏 / 特殊路由（不显示在侧边栏）
    // ══════════════════════════════════════════════════════════
    {
      path: '/student',
      name: '::Menu:StudentPortal',
      layout: eLayoutType.empty,
      invisible: true,
    },
  ]);
}
