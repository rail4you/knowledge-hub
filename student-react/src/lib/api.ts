import axios from 'axios';
import { appConfig } from './config';
import type {
  ApplicationConfiguration,
  Course,
  CourseDetail,
  NewsArticle,
  NewsComment,
  PagedResult,
  Resource,
  ResourceDetail,
  ExerciseDto,
  ChapterProgressDto,
  KnowledgeResourceDto,
  MicroMajorDto,
  MicroMajorDetailDto,
  MicroMajorResourceDto,
} from './types';
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

// === Course Detail APIs ===
export async function getCourseDetail(courseId: string) {
  const { data } = await api.get<CourseDetail>(`/api/app/course/${courseId}/detail`);
  return data;
}

export async function getCourseExercises(courseId: string, chapterId?: string) {
  if (chapterId) {
    const { data } = await api.get<ExerciseDto[]>(`/api/app/exercise/by-chapter/${chapterId}`);
    return data;
  }
  const { data } = await api.get<ExerciseDto[]>(`/api/app/exercise/by-course/${courseId}`);
  return data;
}

export async function getChapterProgress(courseId: string) {
  const { data } = await api.get<{ items: ChapterProgressDto[] }>(`/api/app/student-exercise-record/chapter-progress/${courseId}`);
  return data.items || [];
}

export async function submitExerciseAnswer(input: {
  courseId: string;
  chapterId?: string;
  exerciseId: string;
  studentAnswer: string;
  timeSpentTicks: number;
}) {
  const { data } = await api.post('/api/app/student-exercise-record/save-or-update-record', input);
  return data;
}

export async function getCourseKnowledgeResources(courseId: string) {
  const { data } = await api.get<KnowledgeResourceDto[]>(`/api/app/knowledge-resource/by-course/${courseId}`);
  return data;
}

export async function getKnowledgeGraph(courseId: string) {
  const { data } = await api.get(`/api/app/knowledge-graph/${courseId}`);
  return data;
}

// === Micro Major APIs ===
export async function getPublishedMicroMajors(skipCount = 0, maxResultCount = 20) {
  const { data } = await api.get<PagedResult<MicroMajorDto>>('/api/app/micro-major/published', {
    params: { skipCount, maxResultCount },
  });
  return data;
}

export async function getMicroMajorDetail(id: string) {
  const { data } = await api.get<MicroMajorDetailDto>(`/api/app/micro-major/${id}/detail`);
  return data;
}

export async function getMicroMajorResources(microMajorId: string) {
  const { data } = await api.get<MicroMajorResourceDto[]>(`/api/app/micro-major/resources/${microMajorId}`);
  return data;
}

export async function enrollMicroMajor(microMajorId: string) {
  await api.post(`/api/app/micro-major/enroll/${microMajorId}`);
}

export async function getMyMicroMajorEnrollments() {
  const { data } = await api.get('/api/app/micro-major/my-enrollments');
  return data;
}

// === Resource APIs ===
export async function getResourceDetail(resourceId: string) {
  const { data } = await api.get<ResourceDetail>(`/api/app/resource/${resourceId}`);
  return data;
}

export async function getResourceFileUrl(resourceId: string) {
  const { data } = await api.get<{ url: string }>(`/api/app/resource/file-url/${resourceId}`);
  return data;
}

// === News APIs ===
export async function getPublishedNewsList(skipCount = 0, maxResultCount = 20) {
  const { data } = await api.get<PagedResult<NewsArticle>>('/api/app/news-article/published-list', {
    params: { skipCount, maxResultCount },
  });
  return data;
}

export async function getNewsDetail(id: string) {
  const { data } = await api.get<NewsArticle>(`/api/app/news-article/${id}`);
  return data;
}

export async function getNewsComments(articleId: string) {
  const { data } = await api.get<{ items: NewsComment[] }>(`/api/app/news-comment/approved-list-by-article/${articleId}`);
  return data.items || [];
}

export async function postNewsComment(articleId: string, content: string) {
  const { data } = await api.post('/api/app/news-comment', { articleId, content });
  return data;
}