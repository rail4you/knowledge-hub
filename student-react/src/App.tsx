import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider, useAuth } from './lib/auth';
import { PortalLayout } from './layouts/PortalLayout';
import { PortalHomePage } from './pages/PortalHomePage';
import { TenantPortalPage } from './pages/TenantPortalPage';
import { LoginPage } from './pages/LoginPage';
import { AuthCallbackPage } from './pages/AuthCallbackPage';

// === Old Route Components (kept for backward compatibility during migration) ===
import { StudentLayout } from './layouts/StudentLayout';
import { HomePage } from './pages/HomePage';
import { ResourcesPage } from './pages/ResourcesPage';
import { NewsPage } from './pages/NewsPage';
import { NewsDetailPage } from './pages/NewsDetailPage';
import { AgentTasksPage } from './pages/AgentTasksPage';
import { CoursesPage } from './pages/CoursesPage';
import { CourseDetailPage } from './pages/CourseDetailPage';

import { MicroMajorListPage } from './pages/MicroMajorListPage';
import { MicroMajorDetailPage } from './pages/MicroMajorDetailPage';
import { ResourceDetailPage } from './pages/ResourceDetailPage';
function PlaceholderPage({ title }: { title: string }) {
  return (
    <div className="flex flex-col items-center justify-center min-h-[400px] text-[#999]">
      <div className="text-lg font-semibold mb-2">{title}</div>
      <div className="text-sm">该页面正在开发中...</div>
    </div>
  );
}

function AppRoutes() {
  const auth = useAuth();

  return (
    <Routes>
      {/* ====================================== */}
      {/* Auth routes                            */}
      {/* ====================================== */}
      <Route path="/login" element={<LoginPage />} />
      <Route path="/auth/callback" element={<AuthCallbackPage />} />

      {/* ====================================== */}
      {/* NEW: Portal routes (icve style)        */}
      {/* ====================================== */}
      <Route element={<PortalLayout />}>
        {/* Home */}
        <Route path="/" element={<PortalHomePage />} />

        {/* Tenant portal (Phase 2 - placeholder for now) */}
        <Route path="/tenant/:tenantId" element={<TenantPortalPage />} />
        <Route path="/tenant/:tenantId/resources" element={<PlaceholderPage title="素材列表" />} />
        <Route path="/tenant/:tenantId/resources/:resourceId" element={<ResourceDetailPage />} />
        <Route path="/tenant/:tenantId/courses" element={<CoursesPage />} />
        <Route path="/tenant/:tenantId/courses/:courseId" element={<CourseDetailPage />} />
        <Route path="/tenant/:tenantId/micro-majors" element={<MicroMajorListPage />} />
        <Route path="/tenant/:tenantId/micro-majors/:id" element={<MicroMajorDetailPage />} />
        <Route path="/tenant/:tenantId/news" element={<NewsPage />} />
        <Route path="/tenant/:tenantId/news/:id" element={<NewsDetailPage />} />
      </Route>

      {/* ====================================== */}
      {/* OLD: Student routes (backward compat)  */}
      {/* ====================================== */}
      <Route path="/student" element={
        auth.isAuthenticated ? <StudentLayout /> : <Navigate to="/login" />
      }>
        <Route index element={<Navigate to="resources" />} />
        <Route path="resources" element={<ResourcesPage />} />
        <Route path="news" element={<NewsPage />} />
        <Route path="news/:id" element={<NewsDetailPage />} />
        <Route path="courses" element={<CoursesPage />} />
        <Route path="course-detail/:id" element={<CourseDetailPage />} />
        <Route path="micro-majors" element={<PlaceholderPage title="微专业" />} />
        <Route path="my-micro-majors" element={<PlaceholderPage title="我的微专业" />} />
        <Route path="practicum" element={<PlaceholderPage title="实训项目" />} />
        <Route path="my-practicum" element={<PlaceholderPage title="我的实训" />} />
        <Route path="jobs" element={<PlaceholderPage title="岗位大厅" />} />
        <Route path="my-resumes" element={<PlaceholderPage title="我的简历" />} />
        <Route path="my-applications" element={<PlaceholderPage title="我的投递" />} />
        <Route path="my-guidance" element={<PlaceholderPage title="就业指导" />} />
        <Route path="agent-tasks" element={<AgentTasksPage />} />
        <Route path="agent-tasks/:id" element={<PlaceholderPage title="任务详情" />} />
        <Route path="ai/chat" element={<PlaceholderPage title="智能问答" />} />
        <Route path="ai/lesson-plan" element={<PlaceholderPage title="教案生成" />} />
        <Route path="ai/case-analysis" element={<PlaceholderPage title="案例分析" />} />
        <Route path="ai/career-guidance" element={<PlaceholderPage title="职业规划" />} />
      </Route>

      {/* Compat redirects */}
      <Route path="/resources" element={<HomePage />} />
      <Route path="/student/favorites" element={<Navigate to="/student/resources?tab=favorites" />} />
      <Route path="/student/search" element={<Navigate to="/student/resources?tab=search" />} />
      <Route path="/student/search-history" element={<Navigate to="/student/resources?tab=history" />} />

      {/* Catch-all */}
      <Route path="*" element={<Navigate to="/" />} />
    </Routes>
  );
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <AppRoutes />
      </AuthProvider>
    </BrowserRouter>
  );
}
