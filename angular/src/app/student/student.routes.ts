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
        path: 'practicums',
        loadComponent: () => import('./practicums/student-practicums.component').then(m => m.StudentPracticumsComponent),
        data: {
          name: '实训',
          icon: 'experiment'
        }
      },
      {
        path: 'practicums/:id',
        loadComponent: () => import('./practicums/student-practicum-detail.component').then(m => m.StudentPracticumDetailComponent),
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
            // 关键修复 P1-19：教师端 `/employment/my-guidance`（就业指导）已存在，
            // 学生端一直没有入口。原 routes 把 guidance 漏掉了，导致学生顶栏即使加了 Tab 也会 404。
            // 这里直接复用 my-guidance.component（不区分角色），数据由 EmploymentService.GetMyGuidance
            // 走当前用户过滤。学生看到的是自己被指导的记录。
            path: 'guidance',
            loadComponent: () => import('../employment/my-guidance.component').then(m => m.MyGuidanceComponent),
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
    ]
  }
];