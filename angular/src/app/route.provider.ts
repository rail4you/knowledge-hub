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
      {
        path: '/',
        name: '::Menu:Home',
        iconClass: 'fas fa-home',
        order: 1,
        layout: eLayoutType.application,
      },
      {
        path: '/knowledge-hub',
        name: '::Menu:KnowledgeHub',
        iconClass: 'fas fa-database',
        order: 2,
        layout: eLayoutType.application,
        requiredPolicy: 'KnowledgeHub.Resources'
      },
      {
        path: '/resources',
        name: '::Menu:Resources',
        parentName: '::Menu:KnowledgeHub',
        layout: eLayoutType.application,
        requiredPolicy: 'KnowledgeHub.Resources',
      },
      {
        path: '/search',
        name: '::Menu:Search',
        iconClass: 'fas fa-search',
        order: 3,
        layout: eLayoutType.application,
        requiredPolicy: 'KnowledgeHub.Search',
      },
      {
        path: '/admin/indexing-jobs',
        name: '::Menu:IndexingJobs',
        iconClass: 'fas fa-tasks',
        parentName: '::Menu:KnowledgeHub',
        layout: eLayoutType.application,
        requiredPolicy: 'KnowledgeHub.Resources',
      },
      {
        path: '/ai',
        name: '::Menu:AI',
        iconClass: 'fas fa-robot',
        order: 4,
        layout: eLayoutType.application,
        requiredPolicy: 'KnowledgeHub.AI',
      },
      {
        path: '/ai/chat',
        name: '::Menu:AIChat',
        parentName: '::Menu:AI',
        layout: eLayoutType.application,
        requiredPolicy: 'KnowledgeHub.AI.Chat',
      },
      {
        path: '/ai/lesson-plan',
        name: '::Menu:LessonPlan',
        parentName: '::Menu:AI',
        layout: eLayoutType.application,
        requiredPolicy: 'KnowledgeHub.AI.LessonPlan',
      },
      {
        path: '/ai/case-analysis',
        name: '::Menu:CaseAnalysis',
        parentName: '::Menu:AI',
        layout: eLayoutType.application,
        requiredPolicy: 'KnowledgeHub.AI.CaseAnalysis',
      },
      {
        path: '/ai/career-guidance',
        name: '::Menu:CareerGuidance',
        parentName: '::Menu:AI',
        layout: eLayoutType.application,
        requiredPolicy: 'KnowledgeHub.AI.CareerGuidance',
      },
      {
        path: '/ai/model-management',
        name: '::Menu:ModelManagement',
        parentName: '::Menu:AI',
        layout: eLayoutType.application,
        requiredPolicy: 'KnowledgeHub.AI',
      },
      {
        path: '/admin/exercise',
        name: '::Menu:ExerciseManagement',
        parentName: 'AbpUiNavigation::Menu:Administration',
        layout: eLayoutType.application,
        requiredPolicy: 'KnowledgeHub.Exercises',
      },
      {
        path: '/learning',
        name: '::Menu:Learning',
        iconClass: 'fas fa-graduation-cap',
        order: 5,
        layout: eLayoutType.application,
        requiredPolicy: 'KnowledgeHub.Courses.Default',
      },
      {
        path: '/learning/course-list',
        name: '::Menu:CourseList',
        parentName: '::Menu:Learning',
        layout: eLayoutType.application,
        requiredPolicy: 'KnowledgeHub.Courses.Default',
      },
      {
        path: '/learning/my-courses',
        name: '::Menu:MyCourses',
        parentName: '::Menu:Learning',
        layout: eLayoutType.application,
        requiredPolicy: 'KnowledgeHub.Courses.Default',
      },
      {
        path: '/learning/teacher/create',
        name: '::Menu:CreateCourse',
        parentName: '::Menu:Learning',
        layout: eLayoutType.application,
        requiredPolicy: 'KnowledgeHub.Courses.Create',
      },
      {
        path: '/learning/knowledge-graph/:courseId',
        name: '::Menu:KnowledgeGraph',
        parentName: '::Menu:Learning',
        layout: eLayoutType.application,
        requiredPolicy: 'KnowledgeHub.Courses.Default',
      },
      {
        path: '/student',
        name: '::Menu:StudentPortal',
        layout: eLayoutType.empty,
        invisible: true,
      },
  ]);
}
