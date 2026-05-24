import { BookOpen, ChevronDown, GraduationCap, LibraryBig, LogIn, LogOut, Search, ExternalLink } from 'lucide-react';
import { Outlet, useNavigate, Link } from 'react-router-dom';
import { useEffect } from 'react';
import { setAuthorizationHeader, setTenantId } from '../lib/api';
import { useAuth } from '../lib/auth';

const navItems = [
  { label: '首页', path: '/' },
  { label: '资源库', path: '/resources' },
  { label: '在线精品课', path: '/courses' },
  { label: '实践项目', path: '/projects' },
  { label: '教学资讯', path: '/news' },
];

export function AppShell() {
  const auth = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    void setAuthorizationHeader(auth.getAccessToken);
    void auth.loadTenants();
  }, [auth.getAccessToken, auth.loadTenants]);

  // 当租户改变时更新 API
  useEffect(() => {
    setTenantId(auth.currentTenantId);
  }, [auth.currentTenantId]);

  const handleTenantChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    const value = e.target.value;
    auth.setCurrentTenantId(value || null);
  };

  const handleLogin = () => {
    navigate('/login');
  };

  return (
    <div className="min-h-screen bg-[#f5f7fb] text-slate-950">
      <header className="sticky top-0 z-40 border-b border-slate-200/80 bg-white/95 backdrop-blur">
        <div className="mx-auto flex h-16 max-w-7xl items-center gap-6 px-4 sm:px-6 lg:px-8">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-md bg-blue-600 text-white">
              <GraduationCap className="h-6 w-6" />
            </div>
            <div>
              <div className="text-base font-semibold leading-tight">学生学习中心</div>
              <div className="text-xs text-slate-500">KnowledgeHub Student Portal</div>
            </div>
          </div>

          <nav className="hidden flex-1 items-center gap-1 md:flex">
            {navItems.map(item => (
              <Link
                key={item.path}
                to={item.path}
                className="h-9 rounded-md px-3 text-sm font-medium text-slate-700 hover:bg-slate-100 hover:text-blue-600"
              >
                {item.label}
              </Link>
            ))}
          </nav>

          <div className="hidden h-10 w-72 items-center gap-2 rounded-md border border-slate-200 bg-slate-50 px-3 lg:flex">
            <Search className="h-4 w-4 text-slate-400" />
            <span className="text-sm text-slate-500">搜索课程、资源、资讯</span>
          </div>

          {auth.isAuthenticated ? (
            <div className="flex items-center gap-3">
              {/* 租户选择器 */}
              {auth.tenants.length > 0 && (
                <select
                  value={auth.currentTenantId || ''}
                  onChange={handleTenantChange}
                  className="h-9 rounded-md border border-slate-200 bg-white px-2 text-sm text-slate-700"
                >
                  {auth.tenants.map(t => (
                    <option key={t.id || 'global'} value={t.id || ''}>
                      {t.name}
                    </option>
                  ))}
                </select>
              )}

              <button className="hidden items-center gap-1 rounded-md px-2 py-1.5 text-sm text-slate-700 hover:bg-slate-100 sm:flex">
                {auth.user?.profile?.name || auth.user?.profile?.preferred_username || '学生'}
                <ChevronDown className="h-4 w-4" />
              </button>
              <button
                onClick={() => void auth.logout()}
                className="inline-flex h-9 items-center gap-2 rounded-md border border-slate-200 bg-white px-3 text-sm font-medium text-slate-700 hover:bg-slate-50"
              >
                <LogOut className="h-4 w-4" />
                退出
              </button>
            </div>
          ) : (
            <div className="flex items-center gap-2">
              {/* 租户选择器 */}
              <select
                value={auth.currentTenantId || ''}
                onChange={handleTenantChange}
                className="h-9 rounded-md border border-slate-200 bg-white px-2 text-sm text-slate-700"
              >
                <option value="">选择租户</option>
                {auth.tenants.map(t => (
                  <option key={t.id || 'global'} value={t.id || ''}>
                    {t.name}
                  </option>
                ))}
              </select>
              <button
                onClick={handleLogin}
                className="inline-flex h-9 items-center gap-2 rounded-md bg-blue-600 px-3 text-sm font-medium text-white shadow-sm hover:bg-blue-700"
              >
                <LogIn className="h-4 w-4" />
                登录
              </button>
            </div>
          )}
        </div>
      </header>

      <main>
        <Outlet />
      </main>

      <footer className="border-t border-slate-200 bg-white">
        <div className="mx-auto flex max-w-7xl flex-col gap-3 px-4 py-8 text-sm text-slate-500 sm:px-6 lg:px-8">
          <div className="flex items-center gap-3 text-slate-700">
            <LibraryBig className="h-5 w-5 text-blue-600" />
            <BookOpen className="h-5 w-5 text-blue-600" />
            <span className="font-medium">连接 KnowledgeHub 后端 API 的 React 学生端预览版</span>
          </div>
          <div>当前版本聚焦课程、资源、资讯基础数据连通，后续可逐步替换 Angular 学生端页面。</div>
        </div>
      </footer>
    </div>
  );
}