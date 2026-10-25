import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from '@abp/ng.core';
import { studentPortalGuard } from './student-portal.guard';
import { STUDENT_NAV } from './student-navigation';

/**
 * 学生端路由模块
 * 使用独立布局，不加载 LeptonX 布局
 *
 * 导航元数据统一放在 student-navigation.ts（唯一来源），
 * 这里通过 data: STUDENT_NAV.xxx 引用，侧边栏 / 移动抽屉 / 面包屑
 * 全部由它派生，避免多处手写导航表互相漂移。
 *
 * 权限守卫说明：
 *   · 仅「必须登录」的个性化路由挂 permissionGuard，requiredPolicy 与后端
 *     Student 角色授权项一一对应（见 RolePermissionSeeder）。
 *   · 游客可浏览的路由（课程中心 / 资源库 / 微专业 / 实训 / 就业大厅 / 资讯）
 *     刻意不挂 permissionGuard —— 学生门户要求未登录也能浏览，
 *     真正的数据隔离由后端接口按租户与授权判断。
 */
export const STUDENT_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./layout/student-layout.component').then(m => m.StudentLayoutComponent),
    canActivate: [studentPortalGuard],
    children: [
      {
        path: '',
        redirectTo: 'dashboard',
        pathMatch: 'full'
      },
      {
        path: 'dashboard',
        canActivate: [authGuard],
        loadComponent: () => import('./dashboard/student-dashboard.component').then(m => m.StudentDashboardComponent),
        data: STUDENT_NAV.dashboard,
      },
      {
        path: 'courses',
        loadComponent: () => import('./courses/student-courses.component').then(m => m.StudentCoursesComponent),
        data: STUDENT_NAV.courses,
      },
      {
        path: 'courses/:id',
        loadComponent: () => import('./course-detail/student-course-detail.component').then(m => m.StudentCourseDetailComponent),
      },
      {
        path: 'courses/:id/learn/:chapterId',
        canActivate: [authGuard],
        loadComponent: () => import('./course-learn/student-course-learn.component').then(m => m.StudentCourseLearnComponent),
      },
      {
        path: 'courses/:id/learn',
        canActivate: [authGuard],
        loadComponent: () => import('./course-learn/student-course-learn.component').then(m => m.StudentCourseLearnComponent),
      },
      {
        path: 'resources',
        loadComponent: () => import('./resources/student-resources.component').then(m => m.StudentResourcesComponent),
        data: STUDENT_NAV.resources,
      },
      {
        path: 'resources/:id',
        loadComponent: () => import('./resources/student-resource-detail.component').then(m => m.StudentResourceDetailComponent),
      },
      {
        path: 'my-learning',
        canActivate: [authGuard],
        loadComponent: () => import('./my-learning/student-my-learning.component').then(m => m.StudentMyLearningComponent),
        data: STUDENT_NAV.myLearning,
      },
      {
        // 专项练习：按推荐知识点逐题作答，提交后回写掌握率（智学分析 → 专项练习 的落地页）。
        path: 'practice',
        canActivate: [authGuard],
        loadComponent: () => import('./practice/student-practice.component').then(m => m.StudentPracticeComponent),
        data: STUDENT_NAV.practice,
      },
      {
        // 智学分析：知识点薄弱点、课程学习路径、衔接复习包、专项练习、推送消息。
        // 数据来自真实作答记录（在线练习 + 纸质试卷确认后写入的 AnswerRecord）。
        path: 'analysis',
        canActivate: [authGuard],
        loadComponent: () => import('./analysis/student-analysis.component').then(m => m.StudentAnalysisComponent),
        data: STUDENT_NAV.analysis,
      },
      {
        path: 'favorites',
        canActivate: [authGuard, permissionGuard],
        loadComponent: () => import('./favorites/student-favorites.component').then(m => m.StudentFavoritesComponent),
        data: { ...STUDENT_NAV.favorites, requiredPolicy: 'KnowledgeHub.Resources' },
      },
      {
        path: 'news',
        loadComponent: () => import('./news/student-news.component').then(m => m.StudentNewsComponent),
        data: STUDENT_NAV.news,
      },
      {
        path: 'news/:id',
        loadComponent: () => import('./news/student-news-detail.component').then(m => m.StudentNewsDetailComponent),
      },
      {
        path: 'agent-tasks',
        canActivate: [authGuard, permissionGuard],
        loadComponent: () => import('./agent-tasks/student-agent-task-list.component').then(m => m.StudentAgentTaskListComponent),
        data: { ...STUDENT_NAV.agentTasks, requiredPolicy: 'KnowledgeHub.TeachingAgents' },
      },
      {
        path: 'agent-tasks/:id',
        canActivate: [authGuard],
        loadComponent: () => import('./agent-tasks/student-agent-task-detail.component').then(m => m.StudentAgentTaskDetailComponent),
      },
      {
        path: 'micro-majors',
        loadComponent: () => import('./micro-majors/student-micro-majors.component').then(m => m.StudentMicroMajorsComponent),
        data: STUDENT_NAV.microMajors,
      },
      {
        path: 'micro-majors/:id',
        loadComponent: () => import('./micro-majors/student-micro-major-detail.component').then(m => m.StudentMicroMajorDetailComponent),
      },
      {
        path: 'my-micro-majors',
        canActivate: [authGuard, permissionGuard],
        loadComponent: () => import('./micro-majors/student-my-micro-majors.component').then(m => m.StudentMyMicroMajorsComponent),
        data: { ...STUDENT_NAV.myMicroMajors, requiredPolicy: 'KnowledgeHub.MicroMajors' },
      },
      {
        // 我的证书：集中查看 / 下载微专业结业证书（回答“发的证在哪看”）
        path: 'my-certificates',
        canActivate: [authGuard, permissionGuard],
        loadComponent: () => import('./micro-majors/student-my-certificates.component').then(m => m.StudentMyCertificatesComponent),
        data: { ...STUDENT_NAV.myCertificates, requiredPolicy: 'KnowledgeHub.MicroMajors' },
      },
      {
        path: 'practicums',
        loadComponent: () => import('./practicums/student-practicums.component').then(m => m.StudentPracticumsComponent),
        data: STUDENT_NAV.practicums,
      },
      {
        path: 'practicums/my',
        canActivate: [authGuard, permissionGuard],
        loadComponent: () => import('./practicums/student-my-practicums.component').then(m => m.StudentMyPracticumsComponent),
        data: { ...STUDENT_NAV.myPracticums, requiredPolicy: 'KnowledgeHub.Practicum' },
      },
      {
        path: 'practicums/:id',
        loadComponent: () => import('./practicums/student-practicum-detail.component').then(m => m.StudentPracticumDetailComponent),
      },
      {
        path: 'practicums/:id/chat',
        canActivate: [authGuard],
        loadComponent: () => import('../practicum/practicum-chat.component').then(m => m.PracticumChatComponent),
      },
      {
        // 个人中心：查看自己的信息；联系方式可编辑，密码可修改，其他字段只读。
        path: 'profile',
        canActivate: [authGuard],
        loadComponent: () => import('./profile/student-profile.component').then(m => m.StudentProfileComponent),
        data: STUDENT_NAV.profile,
      },
      {
        // 仿真实训中心：Unity WebGL 镜像列表，只展示 Status=Ready 的资源。
        // 必须声明在 'wasm-center/:slug' 之前，否则列表页会被 :slug 吃掉。
        path: 'wasm-center',
        loadComponent: () => import('./wasm-center/wasm-center-list.component').then(m => m.WasmCenterListComponent),
        data: STUDENT_NAV.wasmCenter,
      },
      {
        // wasm-player 路由保留：可供实训详情页内的 iframe 或直接 URL 访问。
        path: 'wasm-center/:slug',
        loadComponent: () => import('./wasm-center/wasm-player.component').then(m => m.WasmPlayerComponent),
      },
      {
        path: 'employment',
        children: [
          {
            path: '',
            redirectTo: 'jobs',
            pathMatch: 'full',
          },
          {
            path: 'jobs',
            loadComponent: () => import('./employment/student-jobs.component').then(m => m.StudentJobsComponent),
            data: STUDENT_NAV.employmentJobs,
          },
          {
            path: 'jobs/:id',
            loadComponent: () => import('./employment/student-job-detail.component').then(m => m.StudentJobDetailComponent),
          },
          {
            path: 'my-resumes',
            canActivate: [authGuard, permissionGuard],
            loadComponent: () => import('./employment/student-my-resumes.component').then(m => m.StudentMyResumesComponent),
            data: { ...STUDENT_NAV.employmentResumes, requiredPolicy: 'KnowledgeHub.Employment' },
          },
          {
            path: 'my-applications',
            canActivate: [authGuard, permissionGuard],
            loadComponent: () => import('./employment/student-my-applications.component').then(m => m.StudentMyApplicationsComponent),
            data: { ...STUDENT_NAV.employmentApplications, requiredPolicy: 'KnowledgeHub.Employment' },
          },
          {
            path: 'my-outcomes',
            canActivate: [authGuard, permissionGuard],
            loadComponent: () => import('./employment/student-my-outcomes.component').then(m => m.StudentMyOutcomesComponent),
            data: { ...STUDENT_NAV.employmentOutcomes, requiredPolicy: 'KnowledgeHub.Employment' },
          },
          {
            // 学生端就业指导独立页：标题区 + 表格 + 预览/下载弹窗。
            // 教师端 `/employment/my-guidance` 仍复用 my-guidance.component，不受影响。
            path: 'guidance',
            canActivate: [authGuard, permissionGuard],
            loadComponent: () => import('./employment/student-guidance.component').then(m => m.StudentGuidanceComponent),
            data: { ...STUDENT_NAV.employmentGuidance, requiredPolicy: 'KnowledgeHub.Employment' },
          },
        ]
      },
      {
        // 招聘直播 + 课程直播（课程直播需已选对应课程才可见）
        path: 'recruitment-live',
        canActivate: [authGuard],
        loadComponent: () => import('./recruitment-live/student-recruitment-live.component').then(m => m.StudentRecruitmentLiveComponent),
        data: STUDENT_NAV.recruitmentLive,
      },
      {
        path: 'recruitment-live/:id',
        canActivate: [authGuard],
        loadComponent: () => import('../recruitment-live/live-room.component').then(m => m.LiveRoomComponent),
      },
      {
        // AI 助手：包装 ChatComponent，对学生展示"AI 学习助手"标识。
        // 权限 KnowledgeHub.AI.Chat（已在 IdentityDataSeeder 授予 Student 角色）。
        path: 'ai/chat',
        canActivate: [authGuard, permissionGuard],
        loadComponent: () => import('./ai/chat/student-chat.component').then(m => m.StudentChatComponent),
        data: { ...STUDENT_NAV.aiChat, requiredPolicy: 'KnowledgeHub.AI.Chat' },
      },
      {
        // 智能搜索：使用 SearchService 检索已建索引资源，跳到学生端资源详情。
        // 不暴露教师的资源审核、索引管理、视频回放等入口。
        path: 'search',
        loadComponent: () => import('./search/student-search.component').then(m => m.StudentSearchComponent),
        data: STUDENT_NAV.search,
      },
      {
        // 页脚信息页：关于我们 / 帮助中心 / 隐私政策。
        path: 'info/:type',
        loadComponent: () => import('./info/student-info.component').then(m => m.StudentInfoComponent),
      },
      {
        // 特教扩展模块（学生端只读）：适配资源 + 我的 IEP。
        path: 'special-edu/resources',
        loadComponent: () => import('./special-edu/student-special-resources.component').then(m => m.StudentSpecialResourcesComponent),
        data: STUDENT_NAV.specialEduResources,
      },
      {
        path: 'special-edu/my-iep',
        canActivate: [authGuard, permissionGuard],
        loadComponent: () => import('./special-edu/student-my-iep.component').then(m => m.StudentMyIepComponent),
        data: { ...STUDENT_NAV.myIep, requiredPolicy: 'KnowledgeHub.SpecialEducation' },
      },
    ]
  }
];
