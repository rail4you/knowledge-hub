import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import type { PropsWithChildren } from 'react';
import { User, UserManager, WebStorageStateStore } from 'oidc-client-ts';
import { appConfig } from './config';

export type PortalKind = 'student' | 'staff';
const authorityUrl = appConfig.oidcAuthority.replace(/\/$/, '');

interface AuthContextValue {
  user: User | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (tenantId?: string) => Promise<void>;
  loginByAccount: (username: string, password: string, tenantId?: string) => Promise<{ success: boolean; error?: string; portal?: PortalKind }>;
  logout: () => Promise<void>;
  completeLogin: () => Promise<void>;
  getAccessToken: () => Promise<string | null>;
  currentTenantId: string | null;
  setCurrentTenantId: (tenantId: string | null) => void;
  tenants: { id: string | null; name: string }[];
  loadTenants: () => Promise<void>;
  getPortalKind: () => PortalKind;
}

const AuthContext = createContext<AuthContextValue | null>(null);

// Standalone function to get token from storage (for use outside React components)
export async function getAccessTokenFromStorage(): Promise<string | null> {
  try {
    const userManager = createUserManager();
    const user = await userManager.getUser();
    if (user && !user.expired) {
      return user.access_token;
    }
    return null;
  } catch (e) {
    return null;
  }
}

// 获取租户列表
async function fetchTenants(): Promise<{ id: string | null; name: string }[]> {
  const response = await fetch(`${appConfig.apiUrl}/api/public/tenants`);
  const data = await response.json();
  return [{ id: null, name: '全局' }, ...data];
}

function decodeJwtPayload(token: string): Record<string, any> {
  const [, payload] = token.split('.');
  if (!payload) return {};
  const normalized = payload.replace(/-/g, '+').replace(/_/g, '/');
  const padded = normalized.padEnd(Math.ceil(normalized.length / 4) * 4, '=');
  try {
    return JSON.parse(window.atob(padded));
  } catch {
    return {};
  }
}

function normalizeRoles(payload: Record<string, any>): string[] {
  const roleClaim =
    payload.role ??
    payload.roles ??
    payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
  if (!roleClaim) return [];
  return Array.isArray(roleClaim) ? roleClaim : [roleClaim];
}

function portalFromRoles(roles: string[]): PortalKind {
  const normalized = roles.map(role => role.toLowerCase());
  const staffRoles = ['admin', 'leagueadmin', 'schooladmin', 'teacher'];
  return normalized.some(role => staffRoles.includes(role)) ? 'staff' : 'student';
}

function createUserManager(extraQueryParams: Record<string, string> = {}) {
  return new UserManager({
    authority: appConfig.oidcAuthority,
    client_id: appConfig.oidcClientId,
    redirect_uri: `${appConfig.appBaseUrl}/auth/callback`,
    post_logout_redirect_uri: appConfig.appBaseUrl,
    response_type: 'code',
    scope: 'offline_access openid profile email phone roles KnowledgeHub',
    automaticSilentRenew: false,
    userStore: new WebStorageStateStore({ store: window.localStorage }),
    extraQueryParams,
  });
}

export function AuthProvider({ children }: PropsWithChildren) {
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [currentTenantId, setCurrentTenantIdState] = useState<string | null>(() => localStorage.getItem('knowledgehub.tenantId'));
  const [tenants, setTenants] = useState<{ id: string | null; name: string }[]>([]);

  // 创建默认的 UserManager
  const userManager = useMemo(() => createUserManager(), []);

  useEffect(() => {
    let isMounted = true;

    userManager
      .getUser()
      .then((loadedUser) => {
        if (isMounted) {
          setUser(loadedUser);
        }
      })
      .catch(() => {
        if (isMounted) {
          setUser(null);
        }
      })
      .finally(() => {
        if (isMounted) {
          setIsLoading(false);
        }
      });

    const onLoaded = (loadedUser: User) => {
      if (isMounted) {
        setUser(loadedUser);
      }
    };
    const onUnloaded = () => {
      if (isMounted) {
        setUser(null);
      }
    };
    const onAccessTokenExpired = () => {
      if (isMounted) {
        setUser(null);
      }
    };

    userManager.events.addUserLoaded(onLoaded);
    userManager.events.addUserUnloaded(onUnloaded);
    userManager.events.addAccessTokenExpired(onAccessTokenExpired);

    return () => {
      isMounted = false;
      userManager.events.removeUserLoaded(onLoaded);
      userManager.events.removeUserUnloaded(onUnloaded);
      userManager.events.removeAccessTokenExpired(onAccessTokenExpired);
    };
  }, [userManager]);

  const loadTenants = useCallback(async () => {
    try {
      const tenantList = await fetchTenants();
      setTenants(tenantList);
    } catch (error) {
      console.error('Failed to load tenants:', error);
    }
  }, []);

  const setCurrentTenantId = useCallback((tenantId: string | null) => {
    setCurrentTenantIdState(tenantId);
    if (tenantId) {
      localStorage.setItem('knowledgehub.tenantId', tenantId);
    } else {
      localStorage.removeItem('knowledgehub.tenantId');
    }
  }, []);

  // OIDC 登录（通过重定向到授权页面）
  const login = useCallback(async (tenantId?: string) => {
    const effectiveTenantId = tenantId ?? currentTenantId;
    
    const extraQueryParams: Record<string, string> = {};
    if (effectiveTenantId) {
      extraQueryParams['__tenant'] = effectiveTenantId;
    }
    
    const manager = createUserManager(extraQueryParams);
    await manager.signinRedirect();
  }, [currentTenantId]);

  // 直接使用密码 grant 获取 token（不通过重定向）
  const loginByPassword = useCallback(async (
    username: string,
    password: string,
    tenantId?: string
  ): Promise<{ success: boolean; error?: string; portal?: PortalKind }> => {
    try {
      const headers: Record<string, string> = {
        'Content-Type': 'application/x-www-form-urlencoded',
      };
      
      // 使用 __tenant header 传递租户信息
      if (tenantId) {
        headers['__tenant'] = tenantId;
      }

      const params = new URLSearchParams({
        grant_type: 'password',
        client_id: appConfig.oidcClientId,
        username,
        password,
        scope: 'offline_access openid profile email phone roles KnowledgeHub',
      });

      const response = await fetch(`${authorityUrl}/connect/token`, {
        method: 'POST',
        headers,
        body: params.toString(),
      });

      const data = await response.json();

      if (data.access_token) {
        const payload = decodeJwtPayload(data.access_token);
        const roles = normalizeRoles(payload);
        const portal = portalFromRoles(roles);
        // 保存租户 ID
        setCurrentTenantId(tenantId || null);
        
        // 创建用户并保存到 storage
        const userData = {
          access_token: data.access_token,
          refresh_token: data.refresh_token,
          token_type: 'Bearer',
          expires_at: Math.floor(Date.now() / 1000) + data.expires_in,
          scope: data.scope,
          profile: {
            ...payload,
            sub: payload.sub || username,
            iss: payload.iss || '',
            aud: payload.aud || '',
            exp: payload.exp || 0,
            iat: payload.iat || 0,
            email: username,
            username: payload.name || payload.unique_name || username,
            roles,
            portal,
          } as any,
        };
        
        const user = new User(userData);
        await userManager.storeUser(user);
        setUser(user);
        
        return { success: true, portal };
      } else {
        return { success: false, error: data.error_description || '登录失败' };
      }
    } catch (err) {
      console.error('Login error:', err);
      return { success: false, error: '登录失败，请检查网络连接' };
    }
  }, [setCurrentTenantId, userManager]);

  // 保持向后兼容
  const loginByAccount = loginByPassword;

  const logout = useCallback(async () => {
    await userManager.removeUser();
    setUser(null);
    setCurrentTenantId(null);
    window.location.assign(appConfig.appBaseUrl);
  }, [setCurrentTenantId, userManager]);

  const completeLogin = useCallback(async () => {
    const loggedInUser = await userManager.signinRedirectCallback();
    setUser(loggedInUser);
  }, [userManager]);

  const getAccessToken = useCallback(async () => {
    const currentUser = user ?? (await userManager.getUser());
    if (!currentUser || currentUser.expired) return null;
    return currentUser.access_token;
  }, [user, userManager]);

  const getPortalKind = useCallback((): PortalKind => {
    const profile = user?.profile as any;
    if (profile?.portal === 'staff') return 'staff';
    return portalFromRoles(normalizeRoles(profile || {}));
  }, [user]);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isAuthenticated: Boolean(user && !user.expired),
      isLoading,
      login,
      loginByAccount, // alias
      logout,
      completeLogin,
      getAccessToken,
      currentTenantId,
      setCurrentTenantId,
      tenants,
      loadTenants,
      getPortalKind,
    }),
    [completeLogin, getAccessToken, getPortalKind, isLoading, login, logout, user, currentTenantId, tenants, loadTenants, loginByPassword],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used inside AuthProvider');
  }
  return context;
}
