import { useState } from 'react';
import { Link, Outlet, useLocation } from 'react-router-dom';
import {
  Book,
  FileText,
  Calendar,
  LayoutGrid,
  FlaskConical,
  Laptop,
  Briefcase,
  Bot,
  Home,
  ChevronDown,
  LogOut,
  User,
  BookOpen,
  GraduationCap,
} from 'lucide-react';
import { useAuth } from '../lib/auth';

// 课程相关下拉
const courseItems = [
  { path: '/student/courses', icon: Calendar, label: '我的课程' },
  { path: '/student/micro-majors', icon: LayoutGrid, label: '微专业' },
  { path: '/student/my-micro-majors', icon: GraduationCap, label: '我的微专业' },
  { path: '/student/practicum', icon: FlaskConical, label: '实训项目' },
  { path: '/student/my-practicum', icon: Laptop, label: '我的实训' },
];

// 就业相关下拉
const jobItems = [
  { path: '/student/jobs', icon: Briefcase, label: '岗位大厅' },
  { path: '/student/my-resumes', icon: FileText, label: '我的简历' },
  { path: '/student/my-applications', icon: BookOpen, label: '我的投递' },
  { path: '/student/my-guidance', icon: User, label: '就业指导' },
];

// AI 相关下拉
const aiItems = [
  { path: '/student/ai/chat', icon: Bot, label: '智能问答' },
  { path: '/student/ai/lesson-plan', icon: FileText, label: '教案生成' },
  { path: '/student/ai/case-analysis', icon: Briefcase, label: '案例分析' },
  { path: '/student/ai/career-guidance', icon: User, label: '职业规划' },
];

function NavDropdown({
  icon: Icon,
  label,
  items,
}: {
  icon: any;
  label: string;
  items: { path: string; icon: any; label: string }[];
}) {
  const [open, setOpen] = useState(false);

  return (
    <div
      className="nav-dropdown"
      onMouseEnter={() => setOpen(true)}
      onMouseLeave={() => setOpen(false)}
    >
      <Link to={items[0].path} className="nav-link">
        <Icon className="h-4 w-4" />
        <span>{label}</span>
        <ChevronDown className="h-3 w-3 dropdown-arrow" />
      </Link>
      {open && (
        <div className="dropdown-menu">
          {items.map(item => (
            <Link key={item.path} to={item.path} className="dropdown-item">
              <item.icon className="h-4 w-4" />
              <span>{item.label}</span>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}

export function StudentLayout() {
  const location = useLocation();
  const auth = useAuth();
  const [userMenuOpen, setUserMenuOpen] = useState(false);

  const handleLogout = () => {
    auth.logout();
  };

  return (
    <div className="student-layout">
      <header className="app-header">
        <div className="header-container">
          {/* Logo */}
          <Link to="/student/resources" className="brand">
            <div className="brand-logo">
              <svg viewBox="0 0 40 40" fill="none">
                <rect width="40" height="40" rx="8" fill="url(#logo-gradient)" />
                <path d="M12 12h16v4H12v-4zm0 6h16v4H12v-4zm0 6h12v4H12v-4z" fill="white" opacity="0.9" />
                <circle cx="28" cy="28" r="6" fill="white" opacity="0.3" />
                <path d="M26 28l2 2 4-4" stroke="white" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                <defs>
                  <linearGradient id="logo-gradient" x1="0" y1="0" x2="40" y2="40" gradientUnits="userSpaceOnUse">
                    <stop stopColor="#0056D2" />
                    <stop offset="1" stopColor="#1A73E8" />
                  </linearGradient>
                </defs>
              </svg>
            </div>
            <span className="brand-text">易课通</span>
          </Link>

          {/* Navigation */}
          <nav className="main-nav">
            <div className="nav-group">
              {/* 资源中心 — 单一入口，资源库/收藏/搜索都在 tab 里 */}
              <Link
                to="/student/resources"
                className={`nav-link ${location.pathname === '/student/resources' ? 'active' : ''}`}
              >
                <Book className="h-4 w-4" />
                <span>资源中心</span>
              </Link>

              <Link
                to="/student/news"
                className={`nav-link ${location.pathname.startsWith('/student/news') ? 'active' : ''}`}
              >
                <FileText className="h-4 w-4" />
                <span>资讯中心</span>
              </Link>
            </div>

            <div className="nav-divider" />

            {/* 课程下拉 */}
            <NavDropdown icon={Calendar} label="课程学习" items={courseItems} />

            {/* 就业下拉 */}
            <NavDropdown icon={Briefcase} label="就业服务" items={jobItems} />

            {/* AI 下拉 */}
            <NavDropdown icon={Bot} label="AI 助手" items={aiItems} />

            <div className="nav-divider" />

            {/* 任务 */}
            <Link
              to="/student/agent-tasks"
              className={`nav-link ${location.pathname.startsWith('/student/agent-tasks') ? 'active' : ''}`}
            >
              <Bot className="h-4 w-4" />
              <span>课堂任务</span>
            </Link>
          </nav>

          {/* Actions */}
          <div className="header-actions">
            <Link to="/" className="back-link">
              <Home className="h-4 w-4" />
              <span>首页</span>
            </Link>

            <div className="user-menu-container">
              <button
                className="user-avatar"
                onClick={() => setUserMenuOpen(!userMenuOpen)}
                onBlur={() => setTimeout(() => setUserMenuOpen(false), 150)}
              >
                {(auth.user?.profile as any)?.username?.charAt(0)?.toUpperCase() || 'U'}
              </button>
              {userMenuOpen && (
                <div className="user-dropdown">
                  <div className="user-info-item">
                    <div className="user-name">
                      {(auth.user?.profile as any)?.username || '用户'}
                    </div>
                    <div className="user-role">学生</div>
                  </div>
                  <div className="dropdown-divider" />
                  <button className="dropdown-item" onClick={handleLogout}>
                    <LogOut className="h-4 w-4" />
                    <span>退出登录</span>
                  </button>
                </div>
              )}
            </div>
          </div>
        </div>
      </header>

      <main className="main-content">
        <Outlet />
      </main>

      <footer className="app-footer">
        <div className="footer-container">
          <div className="footer-links">
            <a href="#">关于我们</a>
            <a href="#">帮助中心</a>
            <a href="#">隐私政策</a>
            <a href="#">服务条款</a>
          </div>
          <div className="footer-copyright">
            © 2026 易课通资源库系统 - 让学习更高效
          </div>
        </div>
      </footer>
    </div>
  );
}
