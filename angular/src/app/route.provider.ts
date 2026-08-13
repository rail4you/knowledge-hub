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
    //  首页（不显示在侧边栏）
    // ==========================================================
    {
      path: '/',
      name: '::Menu:Home',
      iconClass: 'fas fa-home',
      order: 1,
      layout: eLayoutType.empty,
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
      path: '/favorites',
      name: '::Menu:MyFavorites',
      iconClass: 'fas fa-star',
      parentName: '::Menu:ResourceManagement',
      order: 2,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Search',
    },
    {
      path: '/search',
      name: '::Menu:DocumentSearch',
      iconClass: 'fas fa-search',
      parentName: '::Menu:ResourceManagement',
      order: 3,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Search',
    },
    {
      path: '/my/search-history',
      name: '::Menu:SearchHistory',
      iconClass: 'fas fa-history',
      parentName: '::Menu:ResourceManagement',
      order: 4,
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
    // AI 子功能（教案、案例分析、职业规划）也放在此组
    {
      path: '/ai/lesson-plan',
      name: '::Menu:LessonPlan',
      iconClass: 'fas fa-file-alt',
      parentName: '::Menu:AIManagement',
      order: 4,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.AI.LessonPlan',
    },
    {
      path: '/ai/case-analysis',
      name: '::Menu:CaseAnalysis',
      iconClass: 'fas fa-gavel',
      parentName: '::Menu:AIManagement',
      order: 5,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.AI.CaseAnalysis',
    },
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
      order: 7,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.AI.LessonPlan',
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
      path: '/admin/tenant-info',
      name: '::Menu:TenantInfo',
      iconClass: 'fas fa-building',
      parentName: '::Menu:CourseManagement',
      order: 1,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.TenantInfo.Edit',
    },
    {
      path: '/micro-majors',
      name: '::Menu:MicroMajorsGroup',
      iconClass: 'fas fa-layer-group',
      parentName: '::Menu:CourseManagement',
      order: 2,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.MicroMajors',
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
      path: '/admin/majors',
      name: '::Menu:MajorManagement',
      iconClass: 'fas fa-graduation-cap',
      parentName: '::Menu:CourseManagement',
      order: 4,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Majors.Create',
    },
    {
      path: '/learning/course-list',
      name: '::Menu:CourseList',
      iconClass: 'fas fa-book-open',
      parentName: '::Menu:CourseManagement',
      order: 5,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Courses',
    },
    {
      path: '/learning/student-enrollment',
      name: '::Menu:StudentEnrollment',
      iconClass: 'fas fa-user-plus',
      parentName: '::Menu:CourseManagement',
      order: 6,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Courses.ManageEnrollment',
    },
    {
      path: '/learning/chapter-management',
      name: '::Menu:ChapterManagement',
      iconClass: 'fas fa-sitemap',
      parentName: '::Menu:CourseManagement',
      order: 7,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Courses.Edit',
    },
    {
      path: '/learning/exercise-management',
      name: '::Menu:ExerciseManagement',
      iconClass: 'fas fa-tasks',
      parentName: '::Menu:CourseManagement',
      order: 8,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Courses.Edit',
    },
    {
      path: '/learning/chapter-exercise',
      name: '::Menu:ChapterExercise',
      iconClass: 'fas fa-list-check',
      parentName: '::Menu:CourseManagement',
      order: 9,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Courses.Edit',
    },
    {
      path: '/learning/course-resource',
      name: '::Menu:CourseResource',
      iconClass: 'fas fa-folder-open',
      parentName: '::Menu:CourseManagement',
      order: 10,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Courses.Edit',
    },
    {
      path: '/learning/chapter-resource',
      name: '::Menu:ChapterResource',
      iconClass: 'fas fa-file-lines',
      parentName: '::Menu:CourseManagement',
      order: 11,
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
      path: '/learning/learning-progress',
      name: '::Menu:LearningProgress',
      iconClass: 'fas fa-chart-line',
      parentName: '::Menu:CourseManagement',
      order: 12,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Courses.Edit',
    },
    {
      path: '/learning/learning-statistics',
      name: '::Menu:LearningStatistics',
      iconClass: 'fas fa-chart-bar',
      parentName: '::Menu:CourseManagement',
      order: 13,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Learning.ViewStatistics',
    },

    // ══════════════════════════════════════════════════════════
    //  ④ 实训和就业管理
    // ══════════════════════════════════════════════════════════
    {
      path: '/training-management',
      name: '::Menu:TrainingManagement',
      iconClass: 'fas fa-briefcase',
      order: 5,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Employment',
    },
    {
      path: '/employment/my-guidance',
      name: '::Menu:MyGuidance',
      iconClass: 'fas fa-compass',
      parentName: '::Menu:TrainingManagement',
      order: 5,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Employment.ManageGuidance',
    },

    // ══════════════════════════════════════════════════════════
    //  实训（独立栏目）：实训管理 / 实训任务 / 仿真实训 / 智能体聊天
    // ══════════════════════════════════════════════════════════
    {
      path: '/practicum',
      name: '::Menu:PracticumGroup',
      iconClass: 'fas fa-chalkboard-teacher',
      order: 6,
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
      path: '/admin/recruitment-live',
      name: '::Menu:RecruitmentLive',
      iconClass: 'fas fa-video',
      parentName: '::Menu:TrainingManagement',
      order: 10,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.RecruitmentLive.Create',
    },

    // ══════════════════════════════════════════════════════════
    //  ⑤ 资讯和双高信息管理
    // ══════════════════════════════════════════════════════════
    {
      path: '/info-management',
      name: '::Menu:InfoManagement',
      iconClass: 'fas fa-newspaper',
      order: 6,
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
      path: '/assessment/double-high/report-center',
      name: '::Menu:DoubleHighReportCenter',
      iconClass: 'fas fa-file-export',
      parentName: '::Menu:InfoManagement',
      order: 5,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.DoubleHigh',
    },

    // ══════════════════════════════════════════════════════════
    //  搜索和租户管理
    // ══════════════════════════════════════════════════════════
    {
      path: '/admin-search-tenant',
      name: '::Menu:SearchAndTenantManagement',
      iconClass: 'fas fa-search',
      order: 7,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Search',
    },
    {
      path: '/admin/indexing-jobs',
      name: '::Menu:IndexingJobs',
      iconClass: 'fas fa-tasks',
      parentName: '::Menu:SearchAndTenantManagement',
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Search.ManageIndex',
    },
    {
      path: '/admin/meilisearch',
      name: '::Menu:MeiliSearchDashboard',
      iconClass: 'fas fa-tachometer-alt',
      parentName: '::Menu:SearchAndTenantManagement',
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Search.ManageIndex',
    },
    {
      path: '/admin/search-statistics',
      name: '::Menu:SearchStatistics',
      iconClass: 'fas fa-chart-bar',
      parentName: '::Menu:SearchAndTenantManagement',
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Search.ManageIndex',
    },
    {
      path: '/admin/tenant-info',
      name: '::Menu:TenantInfo',
      iconClass: 'fas fa-building',
      parentName: '::Menu:SearchAndTenantManagement',
      order: 100,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.TenantInfo.Edit',
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
