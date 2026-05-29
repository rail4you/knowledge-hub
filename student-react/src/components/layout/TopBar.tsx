import { useState, useRef, useEffect, useMemo } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import {
  GraduationCap,
  Library,
  BookOpen,
  FileText,
  Newspaper,
  Search,
  User,
  ChevronDown,
  LogOut,
} from 'lucide-react';
import { useAuth } from '../../lib/auth';

const mainNavItems = [
  { path: '/', label: '首页', icon: null },
  { path: 'resources', label: '资源库', icon: Library },
  { path: 'courses', label: '课程', icon: BookOpen },
  { path: 'news', label: '资讯', icon: Newspaper },
];

export function TopBar() {
  const auth = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [userMenuOpen, setUserMenuOpen] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);
  const [searchText, setSearchText] = useState('');
  const menuRef = useRef<HTMLDivElement>(null);
  const searchRef = useRef<HTMLDivElement>(null);

  // Build nav paths with tenant context when available
  const navItems = useMemo(() => {
    const tenantId = auth.currentTenantId;
    return mainNavItems.map(item => ({
      ...item,
      path: item.path === '/' ? '/' : tenantId ? `/tenant/${tenantId}/${item.path}` : `/${item.path}`,
    }));
  }, [auth.currentTenantId]);

  useEffect(() => {
    const handleClick = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) setUserMenuOpen(false);
      if (searchRef.current && !searchRef.current.contains(e.target as Node)) setSearchOpen(false);
    };
    document.addEventListener('mousedown', handleClick);
    return () => document.removeEventListener('mousedown', handleClick);
  }, []);

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    if (searchText.trim()) {
      if (auth.currentTenantId) {
        navigate(`/tenant/${auth.currentTenantId}/resources?search=${encodeURIComponent(searchText.trim())}`);
      } else {
        navigate(`/?search=${encodeURIComponent(searchText.trim())}`);
      }
      setSearchOpen(false);
      setSearchText('');
    }
  };

  const firstLetter = (auth.user?.profile as any)?.username?.charAt(0)?.toUpperCase() || 'U';

  return (
    <header className="sticky top-0 z-50 bg-white border-b border-[#E8E8E8] shadow-sm">
      <div className="mx-auto flex h-16 max-w-[1400px] items-center justify-between px-4 sm:px-6 lg:px-8">
        {/* Logo */}
        <Link to="/" className="flex items-center gap-2.5 shrink-0">
          <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-[#0056D2] text-white">
            <GraduationCap className="h-5 w-5" />
          </span>
          <span className="text-lg font-bold text-[#1a1a1a] tracking-tight">易课通资源库</span>
        </Link>

        {/* Main Nav */}
        <nav className="hidden lg:flex items-center gap-1 ml-8">
          {navItems.map(item => {
            const Icon = item.icon;
            return (
              <Link
                key={item.label}
                to={item.path}
                className="flex items-center gap-1.5 px-4 py-2 rounded-md text-sm font-medium text-[#666] hover:text-[#0056D2] hover:bg-[#F0F6FF] transition-colors"
              >
                {Icon && <Icon className="h-4 w-4" />}
                {item.label}
              </Link>
            );
          })}
        </nav>

        {/* Right actions */}
        <div className="flex items-center gap-3 shrink-0">
          {/* Search */}
          <div ref={searchRef} className="relative">
            <button
              onClick={() => setSearchOpen(!searchOpen)}
              className="flex items-center gap-2 h-9 px-3 rounded-full border border-[#E8E8E8] bg-[#F5F7FA] text-sm text-[#999] hover:border-[#0056D2] hover:text-[#0056D2] transition-colors"
            >
              <Search className="h-4 w-4" />
              <span className="hidden md:inline">搜索课程、资源...</span>
            </button>
            {searchOpen && (
              <form onSubmit={handleSearch} className="absolute right-0 top-full mt-2 w-80 bg-white rounded-lg shadow-lg border border-[#E8E8E8] p-3 z-50">
                <div className="flex items-center gap-2">
                  <Search className="h-4 w-4 text-[#999] shrink-0" />
                  <input
                    type="text"
                    value={searchText}
                    onChange={e => setSearchText(e.target.value)}
                    placeholder="搜索课程、资源、文档..."
                    className="flex-1 border-0 outline-none text-sm text-[#1a1a1a] placeholder:text-[#999]"
                    autoFocus
                  />
                  <button type="submit" className="shrink-0 px-3 py-1.5 rounded-md bg-[#0056D2] text-white text-xs font-medium hover:bg-[#0041A8]">
                    搜索
                  </button>
                </div>
              </form>
            )}
          </div>

          {/* Auth */}
          {auth.isAuthenticated ? (
            <div ref={menuRef} className="relative">
              <button
                onClick={() => setUserMenuOpen(!userMenuOpen)}
                className="flex items-center gap-2 h-9 px-3 rounded-md hover:bg-[#F5F7FA] transition-colors"
              >
                <span className="flex h-7 w-7 items-center justify-center rounded-full bg-[#0056D2] text-white text-xs font-semibold">
                  {firstLetter}
                </span>
                <span className="hidden md:inline text-sm text-[#666]">
                  {(auth.user?.profile as any)?.username || '用户'}
                </span>
                <ChevronDown className="h-3 w-3 text-[#999]" />
              </button>
              {userMenuOpen && (
                <div className="absolute right-0 top-full mt-1 w-44 bg-white rounded-lg shadow-lg border border-[#E8E8E8] py-1 z-50">
                  <div className="px-4 py-2 text-xs text-[#999] border-b border-[#F0F0F0]">
                    {(auth.user?.profile as any)?.username || '用户'}
                  </div>
                  <button
                    onClick={() => { setUserMenuOpen(false); auth.logout(); }}
                    className="w-full flex items-center gap-2 px-4 py-2 text-sm text-[#666] hover:bg-[#F5F7FA] hover:text-[#FF4D4F] transition-colors"
                  >
                    <LogOut className="h-4 w-4" />
                    退出登录
                  </button>
                </div>
              )}
            </div>
          ) : (
            <Link
              to="/login"
              className="inline-flex h-9 items-center rounded-md bg-[#0056D2] px-5 text-sm font-medium text-white hover:bg-[#0041A8] transition-colors"
            >
              登录
            </Link>
          )}
        </div>
      </div>
    </header>
  );
}
