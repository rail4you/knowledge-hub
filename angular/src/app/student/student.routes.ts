import { Routes } from '@angular/router';
import { studentPortalGuard } from './student-portal.guard';

/**
 * 学生端路由模块
 * 使用独立布局，不加载 LeptonX 布局
 */
export const STUDENT_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./layout/student-layout.component').then(m => m.StudentLayoutComponent),
    canActivate: [studentPortalGuard],
    children: [
      {
        path: '',
        redirectTo: 'courses',
        pathMatch: 'full'
      },
      {
        path: 'courses',
        loadComponent: () => import('./courses/student-courses.component').then(m => m.StudentCoursesComponent),
        data: {
          name: '课程中心',
          icon: 'read'
        }
      },
      {
        path: 'courses/:id',
        loadComponent: () => import('./course-detail/student-course-detail.component').then(m => m.StudentCourseDetailComponent),
      },
      {
        path: 'courses/:id/learn/:chapterId',
        loadComponent: () => import('./course-learn/student-course-learn.component').then(m => m.StudentCourseLearnComponent),
      },
      {
        path: 'courses/:id/learn',
        loadComponent: () => import('./course-learn/student-course-learn.component').then(m => m.StudentCourseLearnComponent),
      },
      {
        path: 'resources',
        loadComponent: () => import('./resources/student-resources.component').then(m => m.StudentResourcesComponent),
        data: {
          name: '资源库',
          icon: 'folder-open'
        }
      },
      {
        path: 'resources/:id',
        loadComponent: () => import('./resources/student-resource-detail.component').then(m => m.StudentResourceDetailComponent),
      },
      {
        path: 'my-learning',
        loadComponent: () => import('./my-learning/student-my-learning.component').then(m => m.StudentMyLearningComponent),
        data: {
          name: '我的学习',
          icon: 'line-chart'
        }
      },
      {
        path: 'favorites',
        loadComponent: () => import('./favorites/student-favorites.component').then(m => m.StudentFavoritesComponent),
        data: {
          name: '我的收藏',
          icon: 'star'
        }
      },
      {
        path: 'news',
        loadComponent: () => import('./news/student-news.component').then(m => m.StudentNewsComponent),
        data: {
          name: '新闻资讯',
          icon: 'file-text'
        }
      },
      {
        path: 'news/:id',
        loadComponent: () => import('./news/student-news-detail.component').then(m => m.StudentNewsDetailComponent),
      },
      {
        path: 'agent-tasks',
        loadComponent: () => import('./agent-tasks/student-agent-task-list.component').then(m => m.StudentAgentTaskListComponent),
        data: {
          name: '智能体任务',
          icon: 'robot'
        }
      },
      {
        path: 'agent-tasks/:id',
        loadComponent: () => import('./agent-tasks/student-agent-task-detail.component').then(m => m.StudentAgentTaskDetailComponent),
      },
      {
        path: 'micro-majors',
        loadComponent: () => import('./micro-majors/student-micro-majors.component').then(m => m.StudentMicroMajorsComponent),
        data: {
          name: '微专业',
          icon: 'trophy'
        }
      },
      {
        path: 'micro-majors/:id',
        loadComponent: () => import('./micro-majors/student-micro-major-detail.component').then(m => m.StudentMicroMajorDetailComponent),
      },
      {
        path: 'my-micro-majors',
        loadComponent: () => import('./micro-majors/student-my-micro-majors.component').then(m => m.StudentMyMicroMajorsComponent),
        data: {
          name: '我的微专业',
          icon: 'audit'
        }
      },
      {
        path: 'practicums',
        loadComponent: () => import('./practicums/student-practicums.component').then(m => m.StudentPracticumsComponent),
        data: {
          name: '实训',
          icon: 'experiment'
        }
      },
      {
        path: 'practicums/my',
        loadComponent: () => import('./practicums/student-my-practicums.component').then(m => m.StudentMyPracticumsComponent),
        data: {
          name: '我的实训',
          icon: 'unordered-list'
        }
      },
      {
        path: 'practicums/:id',
        loadComponent: () => import('./practicums/student-practicum-detail.component').then(m => m.StudentPracticumDetailComponent),
      },
      {
        path: 'practicums/:id/chat',
        loadComponent: () => import('../practicum/practicum-chat.component').then(m => m.PracticumChatComponent),
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
            data: {
              name: '就业',
              icon: 'briefcase'
            }
          },
          {
            path: 'jobs/:id',
            loadComponent: () => import('./employment/student-job-detail.component').then(m => m.StudentJobDetailComponent),
          },
          {
            path: 'my-resumes',
            loadComponent: () => import('./employment/student-my-resumes.component').then(m => m.StudentMyResumesComponent),
          },
          {
            path: 'my-applications',
            loadComponent: () => import('./employment/student-my-applications.component').then(m => m.StudentMyApplicationsComponent),
          },
          {
            path: 'my-outcomes',
            loadComponent: () => import('./employment/student-my-outcomes.component').then(m => m.StudentMyOutcomesComponent),
          },
          {
            // 学生端就业指导独立页：标题区 + 表格 + 预览/下载弹窗。
            // 教师端 `/employment/my-guidance` 仍复用 my-guidance.component，不受影响。
            path: 'guidance',
            loadComponent: () => import('./employment/student-guidance.component').then(m => m.StudentGuidanceComponent),
            data: {
              name: '就业指导',
              icon: 'compass'
            }
          },
        ]
      },
      {
        path: 'recruitment-live',
        loadComponent: () => import('./recruitment-live/student-recruitment-live.component').then(m => m.StudentRecruitmentLiveComponent),
        data: {
          name: '招聘直播',
          icon: 'video-camera'
        }
      },
      {
        path: 'recruitment-live/:id',
        loadComponent: () => import('../recruitment-live/live-room.component').then(m => m.LiveRoomComponent),
      },
      {
        // AI 助手：包装 ChatComponent，对学生展示"AI 学习助手"标识。
        // 权限 KnowledgeHub.AI.Chat（已在 IdentityDataSeeder 授予 Student 角色）。
        path: 'ai/chat',
        loadComponent: () => import('./ai/chat/student-chat.component').then(m => m.StudentChatComponent),
        data: {
          name: 'AI 助手',
          icon: 'robot'
        }
      },
      {
        // 智能搜索：使用 SearchService 检索已建索引资源，跳到学生端资源详情。
        // 不暴露教师的资源审核、索引管理、视频回放等入口。
        path: 'search',
        loadComponent: () => import('./search/student-search.component').then(m => m.StudentSearchComponent),
        data: {
          name: '智能搜索',
          icon: 'search'
        }
      },
      {
        // 页脚信息页：关于我们 / 帮助中心 / 隐私政策。
        path: 'info/:type',
        loadComponent: () => import('./info/student-info.component').then(m => m.StudentInfoComponent),
        data: {
          name: '平台信息'
        }
      },
      {
        // 特教扩展模块（学生端只读）：适配资源 + 我的 IEP。
        path: 'special-edu/resources',
        loadComponent: () => import('./special-edu/student-special-resources.component').then(m => m.StudentSpecialResourcesComponent),
        data: {
          name: '特教资源',
          icon: 'heart'
        }
      },
      {
        path: 'special-edu/my-iep',
        loadComponent: () => import('./special-edu/student-my-iep.component').then(m => m.StudentMyIepComponent),
        data: {
          name: '我的IEP',
          icon: 'user'
        }
      },
    ]
  }
];