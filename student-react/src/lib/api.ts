import axios from 'axios';
import { appConfig } from './config';
import type { ApplicationConfiguration, Course, NewsArticle, PagedResult, Resource } from './types';
import { getAccessTokenFromStorage } from './auth';

export const api = axios.create({
  baseURL: appConfig.apiUrl,
  withCredentials: false,
  headers: {
    Accept: 'application/json',
  },
});

// Request interceptor to auto-attach Bearer token
api.interceptors.request.use(
  async (config) => {
    try {
      const token = await getAccessTokenFromStorage();
      if (token) {
        config.headers.Authorization = `Bearer ${token}`;
      }
    } catch (e) {
      // Silently ignore token errors to not block requests
    }
    return config;
  },
  (error) => Promise.reject(error)
);

// 设置租户 ID 的函数
export function setTenantId(tenantId: string | null) {
  if (tenantId) {
    api.defaults.headers.common['__tenant'] = tenantId;
  } else {
    delete api.defaults.headers.common['__tenant'];
  }
}

// Keep old function for backward compatibility (deprecated)
export async function setAuthorizationHeader(getAccessToken: () => Promise<string | null>) {
  // No-op: now handled by interceptor
}

export async function getApplicationConfiguration() {
  const { data } = await api.get<ApplicationConfiguration>('/api/abp/application-configuration');
  return data;
}

export async function getPublishedCourses(maxResultCount = 8) {
  const { data } = await api.get<PagedResult<Course>>('/api/app/course/published', {
    params: { skipCount: 0, maxResultCount },
  });
  return data;
}

export async function getLeagueApprovedResources(maxResultCount = 10) {
  const { data } = await api.get<PagedResult<Resource>>('/api/app/resource/league-approved', {
    params: { skipCount: 0, maxResultCount },
  });
  return data;
}

export async function getPublishedNews(maxResultCount = 6) {
  const { data } = await api.get<PagedResult<NewsArticle>>('/api/app/news-article/published-list', {
    params: { skipCount: 0, maxResultCount },
  });
  return data;
}