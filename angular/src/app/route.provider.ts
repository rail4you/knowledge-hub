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
      requiredPolicy: 'KnowledgeHub.Resources',
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
      requiredPolicy: 'KnowledgeHub.AI.Chat',
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
      path: '/admin/tenant-info',
      name: '::Menu:TenantInfo',
      iconClass: 'fas fa-building',
      parentName: '::Menu:CourseManagement',
      order: 1,
      layout: eLayoutType.application,
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
      requiredPolicy: 'KnowledgeHub.MicroMajors',
    },
    {
      path: '/admin/majors',
      name: '::Menu:MajorManagement',
      iconClass: 'fas fa-graduation-cap',
      parentName: '::Menu:CourseManagement',
      order: 4,
      layout: eLayoutType.application,
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
      iconClass: 'fas fa-pen-to-square',
      parentName: '::Menu:CourseManagement',
      order: 8,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Courses.Edit',
    },
    {
      path: '/learning/chapter-exercise',
      name: '::Menu:ChapterExercise',
      iconClass: 'fas fa-link',
      parentName: '::Menu:CourseManagement',
      order: 9,
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
      requiredPolicy: 'KnowledgeHub.Courses',
      invisible: true,
    },
    {
      path: '/learning/learning-progress',
      name: '::Menu:LearningProgress',
      iconClass: 'fas fa-chart-line',
      parentName: '::Menu:CourseManagement',
      order: 11,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Courses',
    },
    {
      path: '/learning/learning-statistics',
      name: '::Menu:LearningStatistics',
      iconClass: 'fas fa-chart-bar',
      parentName: '::Menu:CourseManagement',
      order: 12,
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
      requiredPolicy: 'KnowledgeHub.Practicum',
    },
    {
      path: '/practicum/projects',
      name: '::Menu:PracticumGroup',
      iconClass: 'fas fa-chalkboard-teacher',
      parentName: '::Menu:TrainingManagement',
      order: 1,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Practicum',
    },
    {
      path: '/admin/practicum/projects',
      name: '::Menu:PracticumManagement',
      iconClass: 'fas fa-tasks',
      parentName: '::Menu:TrainingManagement',
      order: 2,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Practicum',
    },
    {
      path: '/employment/my-applications',
      name: '::Menu:Employment',
      iconClass: 'fas fa-briefcase',
      parentName: '::Menu:TrainingManagement',
      order: 3,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Employment',
    },
    {
      path: '/employment/my-guidance',
      name: '::Menu:MyGuidance',
      iconClass: 'fas fa-compass',
      parentName: '::Menu:TrainingManagement',
      order: 4,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Employment',
    },
    {
      path: '/admin/employment/jobs',
      name: '::Menu:EmploymentJobManagement',
      iconClass: 'fas fa-clipboard-list',
      parentName: '::Menu:TrainingManagement',
      order: 5,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Employment.PublishJob',
    },
    {
      path: '/admin/employment/interviews',
      name: '::Menu:EmploymentInterviewManagement',
      iconClass: 'fas fa-calendar-check',
      parentName: '::Menu:TrainingManagement',
      order: 6,
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
      path: '/admin/recruitment-live',
      name: '::Menu:RecruitmentLive',
      iconClass: 'fas fa-video',
      parentName: '::Menu:TrainingManagement',
      order: 9,
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
      requiredPolicy: 'KnowledgeHub.News',
    },
    {
      path: '/assessment/double-high/projects',
      name: '::Menu:DoubleHighProjects',
      iconClass: 'fas fa-layer-group',
      parentName: '::Menu:InfoManagement',
      order: 3,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.DoubleHigh',
    },
    {
      path: '/assessment/double-high/report-center',
      name: '::Menu:DoubleHighReportCenter',
      iconClass: 'fas fa-file-export',
      parentName: '::Menu:InfoManagement',
      order: 4,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.DoubleHigh',
    },
    {
      path: '/admin/assessment/double-high',
      name: '::Menu:DoubleHighManagement',
      iconClass: 'fas fa-sliders-h',
      parentName: '::Menu:InfoManagement',
      order: 5,
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.DoubleHigh.ManageProject',
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
    },
    {
      path: '/admin/indexing-jobs',
      name: '::Menu:IndexingJobs',
      iconClass: 'fas fa-tasks',
      parentName: '::Menu:SearchAndTenantManagement',
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Resources',
    },
    {
      path: '/admin/meilisearch',
      name: '::Menu:MeiliSearchDashboard',
      iconClass: 'fas fa-tachometer-alt',
      parentName: '::Menu:SearchAndTenantManagement',
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Search.ViewStatistics',
    },
    {
      path: '/admin/search-statistics',
      name: '::Menu:SearchStatistics',
      iconClass: 'fas fa-chart-bar',
      parentName: '::Menu:SearchAndTenantManagement',
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Search.ViewStatistics',
    },
    {
      path: '/admin/exercise',
      name: '::Menu:ExerciseManagement',
      iconClass: 'fas fa-pen-to-square',
      parentName: '::Menu:SearchAndTenantManagement',
      layout: eLayoutType.application,
      requiredPolicy: 'KnowledgeHub.Exercises',
    },
    {
      path: '/admin/tenant-info',
      name: '::Menu:TenantInfo',
      iconClass: 'fas fa-building',
      parentName: '::Menu:SearchAndTenantManagement',
      order: 100,
      layout: eLayoutType.application,
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
