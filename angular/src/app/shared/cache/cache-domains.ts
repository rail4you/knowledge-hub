/**
 * 业务域 → 前端缓存命名空间 映射。
 *
 * 背景：学生端各页面用 ClientCacheService 做 60s 内存缓存（列表/详情/热门等），
 * 后台改数据后如果不同步失效缓存，学生端最多要等 60s 才能看到变化。
 * 这里集中声明「哪个后端 API 写操作会影响哪些缓存」，供
 * cache-invalidation.interceptor 在写请求成功后自动失效。
 *
 * 注意：缓存是浏览器内存级的，只能让「同一浏览器」立即生效；
 * 其它浏览器/设备的用户最长仍需等 60s TTL 自然过期。
 */
export interface CacheDomain {
  /** 后端 API 路径前缀（按 /api/app/{prefix} 前缀匹配） */
  readonly apiPrefixes: readonly string[];
  /** 该域涉及的前端缓存命名空间 */
  readonly namespaces: readonly string[];
}

/** 缓存命名空间常量（与各学生端组件内定义的常量保持一致） */
export const CACHE_NS = {
  /** 公共资讯列表页 /news */
  NEWS: 'news',
  /** 学生端资讯页 */
  STUDENT_NEWS: 'student.news',
  /** 学生端实训 */
  PRACTICUMS: 'student.practicums',
  /** 学生端微专业 */
  MICRO_MAJORS: 'student.micro-majors',
  /** 学生端我的课程 */
  MY_COURSES: 'student.my-courses',
  /** 学生端就业 */
  EMPLOYMENT: 'student.employment',
  /** 学生端资源详情 */
  RESOURCE_DETAIL: 'student.resource-detail',
  /** 学生端课程详情 */
  COURSE_DETAIL: 'student.course-detail',
} as const;

/**
 * 业务域定义。
 * 前缀匹配顺序：长前缀在前（practicum-simulation 先于 practicum），
 * 命中第一个即返回，避免 /api/app/practicum-simulation 被 practicum 抢先匹配。
 */
export const CACHE_DOMAINS: readonly CacheDomain[] = [
  {
    apiPrefixes: ['news-article', 'news-category', 'news-comment', 'news-import'],
    namespaces: [CACHE_NS.NEWS, CACHE_NS.STUDENT_NEWS],
  },
  {
    apiPrefixes: ['practicum-simulation', 'practicum-chat', 'practicum'],
    namespaces: [CACHE_NS.PRACTICUMS],
  },
  {
    apiPrefixes: ['micro-major'],
    namespaces: [CACHE_NS.MICRO_MAJORS, CACHE_NS.MY_COURSES],
  },
  {
    apiPrefixes: ['learning'],
    namespaces: [CACHE_NS.MY_COURSES],
  },
  {
    apiPrefixes: ['employment', 'job-video'],
    namespaces: [CACHE_NS.EMPLOYMENT],
  },
  {
    apiPrefixes: ['resource-media-job', 'resource-recommendation', 'knowledge-resource', 'resource'],
    namespaces: [CACHE_NS.RESOURCE_DETAIL],
  },
  {
    apiPrefixes: ['course-resource', 'chapter-resource', 'chapter', 'course'],
    namespaces: [CACHE_NS.COURSE_DETAIL],
  },
];

/**
 * 根据请求 URL 解析出需要失效的缓存命名空间。
 *
 * @param url 请求 URL（相对或绝对均可）
 * @returns 命中的缓存命名空间；未命中返回空数组
 */
export function resolveCacheNamespaces(url: string): string[] {
  if (!url) return [];

  // 去掉查询串与 hash，只看路径
  const path = url.split('?')[0].split('#')[0];

  for (const domain of CACHE_DOMAINS) {
    for (const prefix of domain.apiPrefixes) {
      if (path.includes(`/api/app/${prefix}`)) {
        return [...domain.namespaces];
      }
    }
  }

  return [];
}
