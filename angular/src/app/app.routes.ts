import { authGuard, permissionGuard, eLayoutType } from '@abp/ng.core';
import { Routes } from '@angular/router';
import { identityUserCreateFormPropContributors, identityUserEntityPropContributors } from './identity-form-prop-contributors';
import { installGuard } from './install/install.guard';
import { STUDENT_ROUTES } from './student/student.routes';
import { studentPortalGuard } from './student/student-portal.guard';

export const APP_ROUTES: Routes = [
  {
    path: 'install',
    loadComponent: () => import('./install/install.component').then(c => c.InstallComponent),
    canActivate: [installGuard],
  },
  {
    path: 'student',
    canActivate: [authGuard, studentPortalGuard],
    loadChildren: () => Promise.resolve(STUDENT_ROUTES),
  },
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./home/portal-home.component').then(c => c.PortalHomeComponent),
    data: {
      layout: eLayoutType.empty
    }
  },
  {
    path: 'account',
    loadChildren: () => import('@abp/ng.account').then(c => c.createRoutes()),
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
    path: 'resources',
    loadComponent: () => import('./resources/resource').then(c => c.ResourceComponent),
    canActivate: [authGuard, permissionGuard],
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
    path: 'admin/indexing-jobs',
    loadComponent: () => import('./admin/indexing-jobs/indexing-jobs.component').then(c => c.IndexingJobsComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Resources',
    },
  },
  {
    path: 'admin/meilisearch',
    loadComponent: () => import('./admin/meilisearch/meilisearch-dashboard.component').then(c => c.MeiliSearchDashboardComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Search.ViewStatistics',
    },
  },
  {
    path: 'admin/search-statistics',
    loadComponent: () => import('./admin/search-statistics/search-statistics.component').then(c => c.SearchStatisticsComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Search.ViewStatistics',
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
    path: 'micro-majors',
    loadComponent: () => import('./micro-majors/micro-major-list.component').then(c => c.MicroMajorListComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.MicroMajors',
    },
  },
  {
    path: 'micro-majors/:id',
    loadComponent: () => import('./micro-majors/micro-major-detail.component').then(c => c.MicroMajorDetailComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.MicroMajors',
    },
  },
  {
    path: 'my/micro-majors',
    loadComponent: () => import('./micro-majors/my-micro-majors.component').then(c => c.MyMicroMajorsComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.MicroMajors',
    },
  },
  {
    path: 'admin/micro-majors',
    loadComponent: () => import('./admin/micro-majors/micro-major-management.component').then(c => c.MicroMajorManagementComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.MicroMajors',
    },
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
    path: 'employment/jobs',
    loadComponent: () => import('./employment/job-list.component').then(c => c.EmploymentJobListComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Employment',
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
    path: 'employment/employer-profile',
    loadComponent: () => import('./employment/employer-profile.component').then(c => c.EmployerProfileComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Employment.PublishJob',
    },
  },
  {
    path: 'employment/my-resumes',
    loadComponent: () => import('./employment/my-resumes.component').then(c => c.MyResumesComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Employment.ManageResume',
    },
  },
  {
    path: 'employment/my-applications',
    loadComponent: () => import('./employment/my-applications.component').then(c => c.MyApplicationsComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Employment',
    },
  },
  {
    path: 'employment/my-guidance',
    loadComponent: () => import('./employment/my-guidance.component').then(c => c.MyGuidanceComponent),
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
    path: 'practicum/projects',
    loadComponent: () => import('./practicum/practicum-list.component').then(c => c.PracticumListComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Practicum',
    },
  },
  {
    path: 'practicum/project/:id',
    loadComponent: () => import('./practicum/practicum-detail.component').then(c => c.PracticumDetailComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Practicum',
    },
  },
  {
    path: 'practicum/my',
    loadComponent: () => import('./practicum/my-practicum.component').then(c => c.MyPracticumComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Practicum',
    },
  },
  {
    path: 'admin/practicum/projects',
    loadComponent: () => import('./admin/practicum/practicum-management.component').then(c => c.PracticumManagementComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.Practicum',
    },
  },
  {
    path: 'admin/news',
    loadComponent: () => import('./admin/news/news-management.component').then(c => c.NewsManagementComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.News',
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
  },
  {
    path: 'ai/chat',
    loadComponent: () => import('./ai/chat/chat.component').then(c => c.ChatComponent),
    canActivate: [authGuard],
  },
  {
    path: 'ai/lesson-plan',
    loadComponent: () => import('./ai/lesson-plan/lesson-plan.component').then(c => c.LessonPlanComponent),
    canActivate: [authGuard],
  },
  {
    path: 'ai/case-analysis',
    loadComponent: () => import('./ai/case-analysis/case-analysis.component').then(c => c.CaseAnalysisComponent),
    canActivate: [authGuard],
  },
  {
    path: 'ai/career-guidance',
    loadComponent: () => import('./ai/career-guidance/career-guidance.component').then(c => c.CareerGuidanceComponent),
    canActivate: [authGuard],
  },
  {
    path: 'ai/model-management',
    loadComponent: () => import('./ai/model-management/model-management.component').then(c => c.ModelManagementComponent),
    canActivate: [authGuard],
  },
  {
    path: 'teaching/agents',
    loadComponent: () => import('./teaching-agents/teaching-agent-list.component').then(c => c.TeachingAgentListComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.TeachingAgents.Manage',
    },
  },
  {
    path: 'teaching/agents/new',
    loadComponent: () => import('./teaching-agents/teaching-agent-editor.component').then(c => c.TeachingAgentEditorComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.TeachingAgents.Manage',
    },
  },
  {
    path: 'teaching/agents/:id',
    loadComponent: () => import('./teaching-agents/teaching-agent-editor.component').then(c => c.TeachingAgentEditorComponent),
    canActivate: [authGuard, permissionGuard],
    data: {
      requiredPolicy: 'KnowledgeHub.TeachingAgents.Manage',
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
    canActivate: [authGuard],
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
        path: 'learning-progress',
        loadComponent: () => import('./learning/learning-progress/learning-progress.component').then(c => c.LearningProgressComponent),
      },
      {
        path: 'learning-statistics',
        loadComponent: () => import('./learning/learning-statistics/learning-statistics.component').then(c => c.LearningStatisticsComponent),
        canActivate: [permissionGuard],
        data: {
          requiredPolicy: 'KnowledgeHub.Learning.ViewStatistics',
        },
      },
    ],
  },
];
