import { authGuard, permissionGuard, eLayoutType } from '@abp/ng.core';
import { Routes } from '@angular/router';
import { identityUserCreateFormPropContributors, identityUserEntityPropContributors } from './identity-form-prop-contributors';
import { installGuard } from './install/install.guard';
import { STUDENT_ROUTES } from './student/student.routes';
import { studentPortalGuard } from './student/student-portal.guard';
import { portalHomeGuard } from './home/portal-home.guard';
import { postLoginGuard } from './auth/post-login.guard';
import { nonStudentGuard } from './auth/non-student.guard';

export const APP_ROUTES: Routes = [
  {
    path: 'install',
    loadComponent: () => import('./install/install.component').then(c => c.InstallComponent),
    canActivate: [installGuard],
  },
  {
    // 登录成功后的统一落点：守卫按角色分发（管理端→工作台，其余→学生门户）。
    // 自研登录页默认 redirectUrl 指向这里，避免登录后停在公开首页。
    path: 'post-login',
    loadComponent: () => import('./auth/post-login.component').then(c => c.PostLoginComponent),
    canActivate: [postLoginGuard],
    data: {
      layout: eLayoutType.empty,
    },
  },
  {
    // 管理端首页：系统工作台，聚合各管理模块统计。
    path: 'admin/workbench',
    loadComponent: () => import('./admin/workbench/workbench.component').then(c => c.WorkbenchComponent),
    canActivate: [authGuard, nonStudentGuard],
    data: {
      layout: eLayoutType.application,
    },
  },
  {
    path: 'student',
    // 这里刻意不挂 authGuard：studentPortalGuard 的语义就是“学生门户公开可浏览”
    // （注释里写明“未登录游客：放行”），外面再叠一层 authGuard 会把它抵消掉，
    // 导致首页精选课程点进去直接跳登录页，游客连课程简介都看不到。
    // 真正需要登录的个性化子路由（我的学习/收藏/简历…）在 student.routes.ts 内单独挂 authGuard。
    canActivate: [studentPortalGuard],
    loadChildren: () => Promise.resolve(STUDENT_ROUTES),
  },
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./home/portal-home.component').then(c => c.PortalHomeComponent),
    // 根本性修复：管理端身份在路由层即时跳到 /resources，组件永远不会被实例化，
    // 不存在"先看到首页再跳走"的时间窗口。之前靠组件 ngOnInit 延迟跳转是根因。
    canActivate: [portalHomeGuard],
    data: {
      layout: eLayoutType.empty
    }
  },
  {
    // 租户资源库列表页（公开）：多校共建专业库列表（主建学校 + 参与建设学校 + 建设负责人）。
    // 必须排在 explore/:section 之前 —— Angular 按顺序匹配，静态段优先于动态段。
    path: 'explore/library',
    loadComponent: () => import('./home/library/library-list.component').then(c => c.LibraryListComponent),
    data: {
      layout: eLayoutType.empty
    }
  },
  {
    // 资源库详情页（公开）：对齐原型 shared-library。
    path: 'library/:id',
    loadComponent: () => import('./home/library/library-detail.component').then(c => c.LibraryDetailComponent),
    data: {
      layout: eLayoutType.empty
    }
  },
  {
    // 公开内容页（课程 / 资源 / 微专业各一个独立页面）：
    // 首页各板块的「查看全部」都指向这里，不再跳学生端页面。
    path: 'explore/:section',
    loadComponent: () => import('./home/explore/explore.component').then(c => c.ExploreComponent),
    data: {
      layout: eLayoutType.empty
    }
  },
  {
    // 院校门户目录（公开）：原型 #campuses，每所学校一个独立门户入口。
    path: 'campuses',
    loadComponent: () => import('./home/campus/campus-directory.component').then(c => c.CampusDirectoryComponent),
    data: {
      layout: eLayoutType.empty
    }
  },
  {
    // 公开课程预览页：游客可访问，无需登录。
    // 用 empty 布局，与门户首页一致，不套学生端侧边栏。
    path: 'course-preview/:id',
    loadComponent: () => import('./home/course-preview.component').then(m => m.CoursePreviewComponent),
    data: {
      layout: eLayoutType.empty
    }
  },
  {
    // 学校简介页（公开）：首页资源库卡片上的校徽 / 校名点击进入。
    path: 'school/:id',
    loadComponent: () => import('./home/school-profile/school-profile.component').then(c => c.SchoolProfileComponent),
    data: {
      layout: eLayoutType.empty
    }
  },
  {
    // 社会学校公开注册页（匿名可访问）：
    // 注册用户统一进入平台唯一的社会学校租户并自动获得 Student 角色。
    path: 'register',
    loadComponent: () => import('./register/register.component').then(c => c.RegisterComponent),
    data: {
      layout: eLayoutType.empty
    }
  },
  {
    path: 'account',
    loadChildren: () => import('@abp/ng.account').then(c => c.createRoutes()),
    // 登录页是自带整站导航/页脚的全幅定制页（LoginComponent），需与首页一样
    // 使用 empty 布局，否则 LeptonX 应用布局的内容容器会约束宽度导致两侧空白对不上。
    // 子路由快照 data 会合并父级 data，ABP account 子路由本身不设 layout，此处生效。
    data: {
      layout: eLayoutType.empty
    }
  },
  {
    // 宿主（全局超级管理员）专用登录网址，租户登录页底部有切换入口。
    // 普通登录页仅展示真实租户 Tab；此地址强制宿主上下文并隐藏租户选择器。
    // （同时支持 /account/login?host=true 作为备用。）
    path: 'admin-login',
    loadComponent: () => import('./login/login.component').then(c => c.LoginComponent),
    data: {
      layout: eLayoutType.empty,
      hostMode: true,
    },
  },
  {
    path: 'identity',
    loadChildren: () =>
      import('@abp/ng.identity').then((m) =>
        m.createRoutes({
          createFormPropContributors: identityUserCreateFormPropContributors,
          entityPropContributors: identityUserEntityPropContributors,
        })
      ),
  },
  {
    path: 'tenant-management',
    loadChildren: () => import('@abp/ng.tenant-management').then(c => c.createRoutes()),
  },
  {
    path: 'admin/edition',
    loadComponent: () => import('./install/edition-info.component').then(c => c.EditionInfoComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Admin',
    },
  },
  {
    path: 'admin/alliance',
    loadComponent: () => import('./admin/alliance/alliance-management.component').then(c => c.AllianceManagementComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Alliance',
    },
  },
  {
    path: 'setting-management',
    loadChildren: () => import('@abp/ng.setting-management').then(c => c.createRoutes()),
  },
  {
    path: 'identity/users/import',
    loadComponent: () => import('./users/import/user-import.component').then(c => c.UserImportComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Users.Import',
    },
  },
  {
    // 多校协同父菜单（LeptonX 对带子项的父菜单仍渲染 routerLink，点击会导航到此处）
    // 补一条重定向路由，避免全局管理员点击“多校协同”菜单项时 404。
    path: 'multi-school-admin',
    redirectTo: 'admin/account-validity',
    pathMatch: 'full',
  },
  {
    path: 'admin/account-validity',
    loadComponent: () => import('./admin/account-validity/account-validity.component').then(c => c.AccountValidityComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.AccountValidity',
    },
  },
  {
    // 学校设置：学校档案（徽标 / 简介 / 联系方式）+ 平台唯一「社会学校」指定（host）。
    path: 'admin/school',
    loadComponent: () => import('./admin/school/school-management.component').then(c => c.SchoolManagementComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Schools',
    },
  },
  {
    // 资源库管理（多校共建：主建学校 + 合作学校成员）。
    path: 'admin/libraries',
    loadComponent: () => import('./admin/libraries/resource-library-management.component').then(c => c.ResourceLibraryManagementComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.ResourceLibraries',
    },
  },
  {
    // 品牌设置（全局管理员专属）：标题/副标题/页脚/Logo，全局唯一。
    path: 'admin/branding',
    loadComponent: () => import('./admin/branding/branding-management.component').then(c => c.BrandingManagementComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Branding',
    },
  },
  {
    // SMTP 邮件设置：租户级（host 上下文为全局），163/QQ 预设 + 测试发送。
    path: 'admin/smtp-settings',
    loadComponent: () => import('./admin/smtp-settings/smtp-settings.component').then(c => c.SmtpSettingsComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Smtp',
    },
  },
  {
    // 存储容量监控：各租户文件容量 / 使用率 / 配额，仅 host 全局管理员（KnowledgeHub.StorageMonitor）。
    path: 'admin/storage-monitor',
    loadComponent: () => import('./admin/storage-monitor/storage-monitor.component').then(c => c.StorageMonitorComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.StorageMonitor',
    },
  },
  {
    // 对象存储设置（阿里云 OSS）：Bucket / Endpoint / AccessKey 维护 + 连通性测试。
    // 凭证落库（加密），与 SMTP 同为租户级配置，host 上下文写全局回退。
    path: 'admin/oss-settings',
    loadComponent: () => import('./admin/oss-settings/oss-settings.component').then(c => c.OssSettingsComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Oss',
    },
  },
  {
    // 资源进度：上传 → 预览 → 院校审核 → 索引 → 联盟审核 → 学生可见。
    // 放在 /admin 下，避免与 '/resources' 前缀冲突（否则会误展开「资源管理」菜单）。
    path: 'admin/resource-progress',
    loadComponent: () => import('./admin/resource-tracking/resource-tracking.component').then(c => c.ResourceTrackingComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Resources',
      layout: eLayoutType.application,
    },
  },
  {
    path: 'resources',
    loadComponent: () => import('./resources/resource').then(c => c.ResourceComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Resources',
      layout: eLayoutType.application,
    },
  },
  {
    // 教师端「教学资源库建设」：查看与本人相关的教学资源库，把本校资源共享进库。
    path: 'teaching-resource-libraries',
    loadComponent: () =>
      import('./resources/teaching-resource-library/teaching-resource-library.component').then(
        c => c.TeachingResourceLibraryComponent,
      ),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.ResourceLibraries',
      layout: eLayoutType.application,
    },
  },
  {
    // 关键修复 P1-12：教师/管理员的"我的收藏"入口。
    // 复用 StudentFavoritesComponent：它内部已经走通用的 /api/app/resource/collected-list
    // 端点（按当前用户过滤），只是硬编码跳 /student/resources/:id，已在组件里改为根据 URL
    // 前缀推断门户路径。nonStudentGuard 阻止学生身份误入此路由。
    path: 'favorites',
    loadComponent: () => import('./student/favorites/student-favorites.component').then(c => c.StudentFavoritesComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Resources',
    },
  },
  {
    path: 'search',
    loadComponent: () => import('./search/search.component').then(c => c.SearchComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Search',
    },
  },
  {
    // 搜索详情：与 /document-viewer/:id 一致的独立全屏页（empty layout），
    // 左侧匹配列表 + 右侧详情，整页占满。
    path: 'search/detail/:id',
    loadComponent: () => import('./search/search-detail/search-detail.component').then(c => c.SearchDetailComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Search',
      layout: eLayoutType.empty,
    },
  },
  {
    // 资源任务：合并媒体处理 + 文档索引 + 视频索引，按资源聚合。
    path: 'admin/resource-tasks',
    loadComponent: () => import('./admin/resource-tasks/resource-task.component').then(c => c.ResourceTaskComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Resources',
    },
  },
  // 旧入口重定向：索引任务 / 媒体处理任务已合并进资源任务。
  {
    path: 'admin/indexing-jobs',
    redirectTo: 'admin/resource-tasks',
    pathMatch: 'full',
  },
  {
    path: 'admin/media-jobs',
    redirectTo: 'admin/resource-tasks',
    pathMatch: 'full',
  },
  {
    path: 'admin/meilisearch',
    loadComponent: () => import('./admin/meilisearch/meilisearch-dashboard.component').then(c => c.MeiliSearchDashboardComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Search.ManageIndex',
    },
  },
  {
    path: 'admin/search-statistics',
    loadComponent: () => import('./admin/search-statistics/search-statistics.component').then(c => c.SearchStatisticsComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Search.ManageIndex',
    },
  },
  {
    path: 'my/search-history',
    loadComponent: () => import('./search/search-history/search-history.component').then(c => c.SearchHistoryComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Search',
    },
  },
  {
    path: 'news',
    loadComponent: () => import('./news/news-list.component').then(c => c.NewsListComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.News',
    },
  },
  {
    path: 'news/:id',
    loadComponent: () => import('./news/news-detail.component').then(c => c.NewsDetailComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.News',
    },
  },
  {
    path: 'admin/majors',
    loadComponent: () => import('./admin/majors/major-management.component').then(c => c.MajorManagementComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Majors.Create',
    },
  },
  {
    path: 'admin/micro-majors',
    loadComponent: () => import('./admin/micro-majors/micro-major-management.component').then(c => c.MicroMajorManagementComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.MicroMajors.Create',
    },
  },
  {
    path: 'admin/tenant-info',
    loadComponent: () => import('./admin/tenant-info/tenant-info-management.component').then(c => c.TenantInfoManagementComponent),
    // 资源库管理：host 全局管理员管理所有租户，租户管理员（SchoolAdmin）管理本租户。
    // 后端 GetListAsync / SaveByTenantIdAsync 已按 CurrentTenant.Id 隔离。
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.TenantInfo.Edit',
    },
  },
  {
    path: 'tenant/:id',
    loadComponent: () => import('./admin/tenant-info/tenant-homepage.component').then(c => c.TenantHomepageComponent),
    // 与 `/` 同理：租户主页也是学生端公开浏览页，管理端身份直接去 /resources。
    canActivate: [portalHomeGuard],
  },

  {
    path: 'assessment/double-high/projects',
    loadComponent: () => import('./double-high/double-high-project-list.component').then(c => c.DoubleHighProjectListComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.DoubleHigh',
    },
  },
  {
    path: 'assessment/double-high/project/:id',
    loadComponent: () => import('./double-high/double-high-project-detail.component').then(c => c.DoubleHighProjectDetailComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.DoubleHigh',
      layout: eLayoutType.application,
    },
  },
  {
    path: 'assessment/double-high/report-center',
    loadComponent: () => import('./double-high/double-high-report-center.component').then(c => c.DoubleHighReportCenterComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.DoubleHigh',
    },
  },
  {
    path: 'admin/assessment/double-high',
    loadComponent: () => import('./admin/double-high/double-high-management.component').then(c => c.DoubleHighManagementComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.DoubleHigh.ManageProject',
    },
  },
  {
    path: 'employment/my-guidance',
    loadComponent: () => import('./admin/employment/admin-employment-guidance.component').then(c => c.AdminEmploymentGuidanceComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Employment.ManageGuidance',
    },
  },
  {
    path: 'employment/jobs/:id',
    loadComponent: () => import('./employment/job-detail.component').then(c => c.EmploymentJobDetailComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Employment',
    },
  },
  {
    path: 'admin/employment/jobs',
    loadComponent: () => import('./admin/employment/employment-job-management.component').then(c => c.EmploymentJobManagementComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Employment.PublishJob',
    },
  },
  {
    path: 'admin/employment/interviews',
    loadComponent: () => import('./admin/employment/employment-interview-management.component').then(c => c.EmploymentInterviewManagementComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Employment.ScheduleInterview',
    },
  },
  {
    path: 'admin/employment/statistics',
    loadComponent: () => import('./admin/employment/employment-statistics.component').then(c => c.EmploymentStatisticsComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Employment.ViewStatistics',
    },
  },
  {
    path: 'admin/employment/outcomes',
    loadComponent: () => import('./admin/employment/employment-outcome-management.component').then(c => c.EmploymentOutcomeManagementComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Employment.ManageOutcome',
    },
  },
  {
    // 招聘直播（就业管理）：与学生面试直播能力一致
    path: 'admin/recruitment-live',
    loadComponent: () => import('./admin/recruitment-live/recruitment-live-management.component').then(c => c.RecruitmentLiveManagementComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.RecruitmentLive.Create',
      liveType: 0,
    },
  },
  {
    path: 'admin/recruitment-live/:id',
    loadComponent: () => import('./recruitment-live/live-room.component').then(c => c.LiveRoomComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.RecruitmentLive.Create',
      liveType: 0,
    },
  },
  {
    // 课程直播（专业和课程管理）：复用招聘直播能力，创建时必须关联租户内已有课程
    path: 'admin/course-live',
    loadComponent: () => import('./admin/recruitment-live/recruitment-live-management.component').then(c => c.RecruitmentLiveManagementComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.RecruitmentLive.Create',
      liveType: 1,
    },
  },
  {
    path: 'admin/course-live/:id',
    loadComponent: () => import('./recruitment-live/live-room.component').then(c => c.LiveRoomComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.RecruitmentLive.Create',
      liveType: 1,
    },
  },
  {
    // P1-20：教师/管理员端的"实训项目"列表——学生应走 /student/practicums，
    // 在此加 nonStudentGuard 把学生重定向到学生门户。
    path: 'practicum/projects',
    loadComponent: () => import('./practicum/practicum-list.component').then(c => c.PracticumListComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Practicum',
      layout: eLayoutType.application,
    },
  },
  {
    // P1-20：教师/管理员端的"实训项目详情"——学生应走 /student/practicums/:id。
    path: 'practicum/project/:id',
    loadComponent: () => import('./practicum/practicum-detail.component').then(c => c.PracticumDetailComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Practicum',
      layout: eLayoutType.application,
    },
  },
  {
    // 实训项目聊天室（教师端）
    path: 'practicum/project/:id/chat',
    loadComponent: () => import('./practicum/practicum-chat.component').then(c => c.PracticumChatComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Practicum',
      layout: eLayoutType.application,
    },
  },
  {
    // P1-20：教师/管理员端的"我的实训"——学生应走学生门户的"我的实训"入口。
    path: 'practicum/my',
    loadComponent: () => import('./practicum/my-practicum.component').then(c => c.MyPracticumComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Practicum',
      layout: eLayoutType.application,
    },
  },
  {
    // P1-20：实训项目管理——只允许非学生角色。
    path: 'admin/practicum/projects',
    loadComponent: () => import('./admin/practicum/practicum-management.component').then(c => c.PracticumManagementComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Practicum.Create',
    },
  },
  {
    // 实训任务：在已有实训上管理任务，并基于任务进行指导与评分。
    path: 'admin/practicum/tasks',
    loadComponent: () => import('./admin/practicum/practicum-tasks.component').then(c => c.PracticumTasksComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Practicum',
    },
  },
  {
    // 仿真实训：基于实训的仿真镜像管理。
    path: 'admin/practicum/simulations',
    loadComponent: () => import('./admin/practicum/practicum-simulations.component').then(c => c.PracticumSimulationsComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Practicum',
    },
  },
  {
    // 实训智能体聊天：实训沟通 + 智能体配置。
    path: 'admin/practicum/chat',
    loadComponent: () => import('./admin/practicum/practicum-agent-chat.component').then(c => c.PracticumAgentChatComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Practicum',
    },
  },
  // 路由已移除：/admin/wasm-mirrors 及 /wasm-management 重定向
  {
    path: 'admin/news',
    loadComponent: () => import('./admin/news/news-management.component').then(c => c.NewsManagementComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.News.Create',
    },
  },
  {
    path: 'admin/exercise',
    loadComponent: () => import('./admin/exercise/exercise-management.component').then(c => c.ExerciseManagementComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Exercises',
    },
  },
  {
    path: 'document-viewer/:id',
    loadComponent: () => import('./document-viewer/document-viewer.component').then(c => c.DocumentViewerComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Resources',
    },
  },
  {
    path: 'ai/chat',
    loadComponent: () => import('./ai/chat/chat.component').then(c => c.ChatComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.AI',
    },
  },
  {
    path: 'ai/lesson-plan',
    loadComponent: () => import('./ai/lesson-plan/lesson-plan.component').then(c => c.LessonPlanComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.AI.LessonPlan',
    },
  },
  {
    path: 'ai/case-analysis',
    loadComponent: () => import('./ai/case-analysis/case-analysis.component').then(c => c.CaseAnalysisComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.AI.CaseAnalysis',
    },
  },
  {
    path: 'ai/career-guidance',
    loadComponent: () => import('./ai/career-guidance/career-guidance.component').then(c => c.CareerGuidanceComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.AI.CareerGuidance',
    },
  },
  {
    path: 'ai/exercise-generate',
    loadComponent: () => import('./ai/exercise-generate/exercise-generate.component').then(c => c.ExerciseGenerateComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.AI.ExerciseGenerate',
    },
  },
  {
    path: 'ai/image-generation',
    loadComponent: () => import('./ai/image-generation/image-generation.component').then(c => c.ImageGenerationComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.AI.ImageGeneration',
    },
  },
  {
    path: 'ai/video-generation',
    loadComponent: () => import('./ai/video-generation/video-generation.component').then(c => c.VideoGenerationComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.AI.VideoGeneration',
    },
  },
  {
    path: 'ai/model-management',
    loadComponent: () => import('./ai/model-management/model-management.component').then(c => c.ModelManagementComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.AI.LessonPlan',
    },
  },
  {
    path: 'ai/tasks',
    loadComponent: () => import('./ai/tasks/ai-task-center.component').then(c => c.AiTaskCenterComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.AI',
    },
  },
  {
    path: 'teaching/agents',
    loadComponent: () => import('./teaching-agents/teaching-agent-list.component').then(c => c.TeachingAgentListComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.TeachingAgents.Manage',
    },
  },
  // ════════════ 特殊教育扩展模块（独立分组，插件式开关） ════════════
  {
    path: 'special-edu/teaching-design',
    loadComponent: () => import('./special-edu/teaching-design/teaching-design.component').then(c => c.TeachingDesignComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.SpecialEducation.TeachingDesign',
    },
  },
  {
    path: 'special-edu/iep',
    loadComponent: () => import('./special-edu/iep/iep.component').then(c => c.IepComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.SpecialEducation.IEP',
    },
  },
  {
    path: 'special-edu/resources',
    loadComponent: () => import('./special-edu/resources/special-resources.component').then(c => c.SpecialResourcesComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.SpecialEducation.Resource',
    },
  },
  {
    path: 'special-edu/braille',
    loadComponent: () => import('./special-edu/braille/braille-study.component').then(c => c.BrailleStudyComponent),
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.SpecialEducation.Resource',
    },
  },
  {
    path: 'admin/special-education',
    loadComponent: () => import('./special-edu/admin/special-edu-admin.component').then(c => c.SpecialEduAdminComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.SpecialEducation.Manage',
    },
  },
  {
    path: 'admin/voice-assistant',
    loadComponent: () => import('./admin/voice-assistant/voice-assistant-admin.component').then(c => c.VoiceAssistantAdminComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.VoiceAssistant.Manage',
    },
  },
  {
    path: 'teaching/agent-tasks',
    loadComponent: () => import('./teaching-agents/teaching-agent-task-list.component').then(c => c.TeachingAgentTaskListComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.TeachingAgents.Assign',
    },
  },
  {
    path: 'teaching/agent-tasks/:id',
    loadComponent: () => import('./teaching-agents/teaching-agent-task-detail.component').then(c => c.TeachingAgentTaskDetailComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.TeachingAgents.Assign',
    },
  },
  {
    path: 'learning',
    canActivate: [authGuard, nonStudentGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Courses',
    },
    children: [
      {
        path: 'my-courses',
        loadComponent: () => import('./learning/my-courses/my-courses.component').then(c => c.MyCoursesComponent),
      },
      {
        path: 'course-list',
        loadComponent: () => import('./learning/course-list/course-list.component').then(c => c.CourseListComponent),
      },
      {
        path: 'student-enrollment',
        loadComponent: () => import('./learning/student-enrollment/student-enrollment.component').then(c => c.StudentEnrollmentComponent),
      },
      {
        path: 'chapter-management',
        loadComponent: () => import('./learning/chapter-management/chapter-management.component').then(c => c.ChapterManagementComponent),
      },
      {
        path: 'exercise-management',
        loadComponent: () => import('./learning/exercise-management/exercise-management.component').then(c => c.ExerciseManagementComponent),
      },
      {
        path: 'chapter-exercise',
        loadComponent: () => import('./learning/chapter-exercise/chapter-exercise.component').then(c => c.ChapterExerciseComponent),
      },
      {
        path: 'course-resource',
        loadComponent: () => import('./learning/course-resource/course-resource.component').then(c => c.CourseResourceComponent),
      },
      {
        path: 'chapter-resource',
        loadComponent: () => import('./learning/chapter-resource/chapter-resource.component').then(c => c.ChapterResourceComponent),
      },
      {
        path: 'course-detail/:id',
        loadComponent: () => import('./learning/course-detail/course-detail.component').then(c => c.CourseDetailComponent),
      },
      {
        path: 'teacher/create',
        loadComponent: () => import('./learning/teacher/course-create/course-create.component').then(c => c.CourseCreateComponent),
      },
      {
        path: 'knowledge-graph/:courseId',
        loadComponent: () => import('./learning/knowledge-graph/knowledge-graph.component').then(c => c.KnowledgeGraphComponent),
      },
      {
        path: 'exercise/:courseId',
        loadComponent: () => import('./learning/exercise/exercise-list.component').then(c => c.ExerciseListComponent),
      },
      {
        path: 'exercise-practice/:courseId',
        loadComponent: () => import('./learning/exercise/exercise-practice.component').then(c => c.ExercisePracticeComponent),
      },
      {
        path: 'exercise-learning/:courseId',
        loadComponent: () => import('./learning/exercise-learning/exercise-learning.component').then(c => c.ExerciseLearningComponent),
      },
      {
        path: 'learning-statistics',
        loadComponent: () => import('./learning/learning-statistics/learning-statistics.component').then(c => c.LearningStatisticsComponent),
        canActivate: [permissionGuard],
        data: {
          requiredPolicy: 'KnowledgeHub.Learning.ViewStatistics',
        },
      },
      {
        // 智学分析（教师端）：拆成三个独立页面（各自一条路由，侧边栏直接点进去）
        //   /learning/analysis/paper    试卷分析（上传 · OCR · 校对 · 推送）
        //   /learning/analysis/report   分析报告（班级薄弱点 · 教学诊断 · 导出）
        //   /learning/analysis/linkage  课程联动设置
        // 权限沿用父级 KnowledgeHub.Courses。
        path: 'analysis',
        children: [
          {
            path: '',
            pathMatch: 'full',
            redirectTo: 'paper',
          },
          {
            path: 'paper',
            data: { section: 'paper' },
            loadComponent: () =>
              import('./learning/analysis/learning-analysis.component').then(c => c.LearningAnalysisComponent),
          },
          {
            path: 'report',
            data: { section: 'report' },
            loadComponent: () =>
              import('./learning/analysis/learning-analysis.component').then(c => c.LearningAnalysisComponent),
          },
          {
            path: 'linkage',
            data: { section: 'linkage' },
            loadComponent: () =>
              import('./learning/analysis/learning-analysis.component').then(c => c.LearningAnalysisComponent),
          },
        ],
      },
      {
        // 知识点管理：归一化闭环（确认灰区 / 合并 / 拆分）+ 习题关联。
        path: 'knowledge-points',
        loadComponent: () =>
          import('./learning/knowledge-points/knowledge-point-management.component').then(
            c => c.KnowledgePointManagementComponent
          ),
      },
    ],
  },
];

// Last modified: trigger dev server reload

