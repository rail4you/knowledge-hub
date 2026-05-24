import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider, useAuth } from './lib/auth';
import { StudentLayout } from './layouts/StudentLayout';
import { HomePage } from './pages/HomePage';
import { LoginPage } from './pages/LoginPage';
import { AuthCallbackPage } from './pages/AuthCallbackPage';
import { ResourcesPage } from './pages/ResourcesPage';
import { NewsPage } from './pages/NewsPage';
import { NewsDetailPage } from './pages/NewsDetailPage';
import { AgentTasksPage } from './pages/AgentTasksPage';

// Placeholder components for pages not yet implemented
function PlaceholderPage({ title }: { title: string }) {
  return (
    <div className="student-page">
      <div className="empty-state">
        <div className="empty-state-title">{title}</div>
        <div className="empty-state-desc">该页面正在开发中...</div>
      </div>
    </div>
  );
}

function AppRoutes() {
  const auth = useAuth();

  return (
    <Routes>
      {/* 公开路由 */}
      <Route path="/login" element={<LoginPage />} />
      <Route path="/auth/callback" element={<AuthCallbackPage />} />

      {/* 学生端布局 - 所有学生页面都通过 /student/* 访问 */}
      <Route path="/student" element={
        auth.isAuthenticated ? <StudentLayout /> : <Navigate to="/login" />
      }>
        <Route index element={<Navigate to="resources" />} />
        {/* 资源中心 — 包含资源库/我的收藏/资源搜索/搜索历史 四个 tab */}
        <Route path="resources" element={<ResourcesPage />} />
        <Route path="news" element={<NewsPage />} />
        <Route path="news/:id" element={<NewsDetailPage />} />
        <Route path="courses" element={<PlaceholderPage title="我的课程" />} />
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

      {/* 首页 - 独立的简单首页 */}
      <Route path="/" element={
        auth.isAuthenticated ? <HomePage /> : <Navigate to="/login" />
      } />

      {/* 兼容旧链接：重定向到资源中心 */}
      <Route path="/student/favorites" element={<Navigate to="/student/resources?tab=favorites" />} />
      <Route path="/student/search" element={<Navigate to="/student/resources?tab=search" />} />
      <Route path="/student/search-history" element={<Navigate to="/student/resources?tab=history" />} />

      {/* 默认重定向 */}
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
