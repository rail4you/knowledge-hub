import { authGuard, permissionGuard } from '@abp/ng.core';
import { Routes } from '@angular/router';

export const APP_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./home/home.component').then(c => c.HomeComponent),
  },
  {
    path: 'account',
    loadChildren: () => import('@abp/ng.account').then(c => c.createRoutes()),
  },
  {
    path: 'identity',
    loadChildren: () => import('@abp/ng.identity').then(c => c.createRoutes()),
  },
  {
    path: 'tenant-management',
    loadChildren: () => import('@abp/ng.tenant-management').then(c => c.createRoutes()),
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
    path: 'student',
    loadComponent: () => import('./student/layout/student-layout.component').then(c => c.StudentLayoutComponent),
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'resources', pathMatch: 'full' },
      { path: 'resources', loadComponent: () => import('./student/resources/student-resources.component').then(c => c.StudentResourcesComponent) },
      { path: 'search', loadComponent: () => import('./search/search.component').then(c => c.SearchComponent) },
      { path: 'ai/chat', loadComponent: () => import('./ai/chat/chat.component').then(c => c.ChatComponent) },
      { path: 'ai/lesson-plan', loadComponent: () => import('./ai/lesson-plan/lesson-plan.component').then(c => c.LessonPlanComponent) },
      { path: 'ai/case-analysis', loadComponent: () => import('./ai/case-analysis/case-analysis.component').then(c => c.CaseAnalysisComponent) },
      { path: 'ai/career-guidance', loadComponent: () => import('./ai/career-guidance/career-guidance.component').then(c => c.CareerGuidanceComponent) },
      { path: 'courses', loadComponent: () => import('./learning/my-courses/my-courses.component').then(c => c.MyCoursesComponent) },
    ]
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
        path: 'course-detail/:id',
        loadComponent: () => import('./learning/course-detail/course-detail.component').then(c => c.CourseDetailComponent),
      },
      {
        path: 'teacher/create',
        loadComponent: () => import('./learning/teacher/course-create/course-create.component').then(c => c.CourseCreateComponent),
      },
    ],
  },
];
