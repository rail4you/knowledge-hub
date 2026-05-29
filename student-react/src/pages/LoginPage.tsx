import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Building2, GraduationCap, Loader2 } from 'lucide-react';
import { useAuth } from '../lib/auth';
import { appConfig } from '../lib/config';

export function LoginPage() {
  const auth = useAuth();
  const navigate = useNavigate();
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [selectedTenant, setSelectedTenant] = useState('');

  // 加载租户列表
  useEffect(() => {
    auth.loadTenants();
  }, []);

  // 如果已经登录，跳转到首页
  useEffect(() => {
    if (auth.isAuthenticated) {
      if (auth.getPortalKind() === 'staff') {
        window.location.assign(appConfig.adminUrl);
      } else {
        navigate('/');
      }
    }
  }, [auth, auth.isAuthenticated, navigate]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setIsLoading(true);

    try {
      const result = await auth.loginByAccount(username, password, selectedTenant || undefined);
      
      if (!result.success) {
        setError(result.error || '登录失败');
        setIsLoading(false);
      } else {
        if (result.portal === 'staff') {
          window.location.assign(appConfig.adminUrl);
        } else {
          navigate('/');
        }
      }
    } catch (err) {
      setError('登录失败，请检查网络连接');
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-[linear-gradient(135deg,#0b5fc4_0%,#0f8fbe_48%,#13a67d_100%)] flex items-center justify-center p-4">
      <div className="w-full max-w-md">
        {/* Logo */}
        <div className="flex justify-center mb-8">
          <div className="flex items-center gap-3 text-white">
            <div className="flex h-14 w-14 items-center justify-center rounded-xl bg-white/20 backdrop-blur">
              <GraduationCap className="h-8 w-8" />
            </div>
            <div>
              <div className="text-2xl font-bold">易课通资源库</div>
              <div className="text-sm text-white/80">统一登录入口</div>
            </div>
          </div>
        </div>

        {/* 登录卡片 */}
        <div className="rounded-2xl bg-white shadow-2xl p-8">
          <h2 className="text-2xl font-semibold text-slate-900 text-center mb-6">登录账号</h2>

          {error && (
            <div className="mb-4 rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">
              {error}
            </div>
          )}

          <form onSubmit={handleSubmit} className="space-y-5">
            {/* 租户选择 */}
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1.5">租户</label>
              <div className="relative">
                <Building2 className="pointer-events-none absolute left-3 top-3.5 h-4 w-4 text-slate-400" />
              <select
                value={selectedTenant}
                onChange={e => setSelectedTenant(e.target.value)}
                className="w-full h-11 rounded-lg border border-slate-300 pl-9 pr-3 text-sm focus:border-blue-500 focus:ring-2 focus:ring-blue-500/20 outline-none transition"
              >
                {auth.tenants.map(t => (
                  <option key={t.id || 'global'} value={t.id || ''}>
                    {t.name}
                  </option>
                ))}
              </select>
              </div>
            </div>

            {/* 用户名 */}
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1.5">用户名</label>
              <input
                type="text"
                value={username}
                onChange={e => setUsername(e.target.value)}
                placeholder="请输入用户名"
                required
                className="w-full h-11 rounded-lg border border-slate-300 px-3 text-sm focus:border-blue-500 focus:ring-2 focus:ring-blue-500/20 outline-none transition"
              />
            </div>

            {/* 密码 */}
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1.5">密码</label>
              <input
                type="password"
                value={password}
                onChange={e => setPassword(e.target.value)}
                placeholder="请输入密码"
                required
                className="w-full h-11 rounded-lg border border-slate-300 px-3 text-sm focus:border-blue-500 focus:ring-2 focus:ring-blue-500/20 outline-none transition"
              />
            </div>

            {/* 提交按钮 */}
            <button
              type="submit"
              disabled={isLoading}
              className="w-full h-11 rounded-lg bg-blue-600 text-white font-medium hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed transition flex items-center justify-center gap-2"
            >
              {isLoading ? (
                <>
                  <Loader2 className="h-4 w-4 animate-spin" />
                  登录中...
                </>
              ) : (
                '登录'
              )}
            </button>
          </form>

          <div className="mt-6 text-center text-xs text-slate-500">
            <p>学生账号进入 React 学生端，教师和管理账号进入 Angular 管理端。</p>
          </div>
        </div>
      </div>
    </div>
  );
}
