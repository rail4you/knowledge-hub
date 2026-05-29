import { useQueries } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import {
  ArrowRight,
  BookOpen,
  Clock3,
  FileText,
  Flame,
  GraduationCap,
  LibraryBig,
  PlayCircle,
  Search,
  Star,
  TrendingUp,
  Users,
  Bot,
} from 'lucide-react';
import { getLeagueApprovedResources, getPublishedCourses, getPublishedNews } from '../lib/api';
import { useAuth } from '../lib/auth';
import { compactNumber, formatDate } from '../lib/utils';
import type { Course, NewsArticle, Resource } from '../lib/types';

const quickStats = [
  { label: '精品课程', value: '课程中心', icon: GraduationCap },
  { label: '教学资源', value: '资源库', icon: LibraryBig },
  { label: '热门学习', value: '排行榜', icon: TrendingUp },
  { label: '学习记录', value: '个人中心', icon: Clock3 },
];

// Tab navigation items for student portal
const tabNavigation = [
  { path: '/student/resources', label: '资源中心', icon: LibraryBig },
  { path: '/student/news', label: '资讯中心', icon: FileText },
  { path: '/student/resources?tab=search', label: '搜索', icon: Search },
  { path: '/student/resources?tab=favorites', label: '我的收藏', icon: Star },
  { path: '/student/agent-tasks', label: '课堂任务', icon: Bot },
];

export function HomePage() {
  const auth = useAuth();
  const [coursesQuery, resourcesQuery, newsQuery] = useQueries({
    queries: [
      { queryKey: ['courses', 'published'], queryFn: () => getPublishedCourses(8) },
      { queryKey: ['resources', 'league-approved'], queryFn: () => getLeagueApprovedResources(10) },
      { queryKey: ['news', 'published'], queryFn: () => getPublishedNews(6) },
    ],
  });

  const courses = coursesQuery.data?.items ?? [];
  const resources = resourcesQuery.data?.items ?? [];
  const news = newsQuery.data?.items ?? [];
  const isLoading = coursesQuery.isLoading || resourcesQuery.isLoading || newsQuery.isLoading;
  const hasError = coursesQuery.isError || resourcesQuery.isError || newsQuery.isError;

  return (
    <>
      <header className="bg-white border-b border-slate-200">
        <div className="mx-auto flex h-16 max-w-7xl items-center justify-between px-4 sm:px-6 lg:px-8">
          <Link to="/" className="flex items-center gap-3">
            <span className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary text-white">
              <GraduationCap className="h-6 w-6" />
            </span>
            <span className="text-lg font-semibold text-slate-950">易课通资源库</span>
          </Link>
          <div className="flex items-center gap-3">
            {auth.isAuthenticated ? (
              <Link to="/student" className="inline-flex h-10 items-center rounded-md bg-primary px-4 text-sm font-medium text-white">
                进入学习中心
              </Link>
            ) : (
              <Link to="/login" className="inline-flex h-10 items-center rounded-md bg-primary px-4 text-sm font-medium text-white">
                登录
              </Link>
            )}
          </div>
        </div>
      </header>

      {/* Tab Navigation */}
      <nav className="bg-white border-b border-slate-200 sticky top-0 z-40">
        <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
          <div className="flex gap-1 overflow-x-auto py-2">
            {tabNavigation.map(item => {
              const Icon = item.icon;
              return (
                <Link
                  key={item.path}
                  to={auth.isAuthenticated ? item.path : (item.path.includes('favorites') || item.path.includes('agent-tasks') ? '/login' : '/resources')}
                  className="flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium text-slate-600 hover:text-primary hover:bg-slate-50 transition whitespace-nowrap"
                >
                  <Icon className="h-4 w-4" />
                  {item.label}
                </Link>
              );
            })}
          </div>
        </div>
      </nav>

      <section className="bg-[linear-gradient(135deg,#0b6bcb_0%,#0f8fbe_46%,#18a999_100%)] text-white">
        <div className="mx-auto grid min-h-[360px] max-w-7xl gap-8 px-4 py-10 sm:px-6 lg:grid-cols-[1.08fr_0.92fr] lg:px-8">
          <div className="flex flex-col justify-center">
            <div className="mb-5 inline-flex w-fit items-center gap-2 rounded-full border border-white/25 bg-white/15 px-3 py-1 text-sm backdrop-blur">
              <Flame className="h-4 w-4 text-amber-200" />
              职业教育资源库
            </div>
            <h1 className="max-w-3xl text-4xl font-semibold tracking-normal sm:text-5xl">
              面向职业教育的资源库首页
            </h1>
            <p className="mt-5 max-w-2xl text-base leading-7 text-white/88">
              游客可以预览公开资源、课程和资讯；登录后学生进入 React 学习中心，教师和管理端进入原 Angular 工作台。
            </p>

            <div className="mt-8 flex max-w-2xl items-center gap-2 rounded-lg bg-white p-2 shadow-xl shadow-sky-950/20">
              <Search className="ml-3 h-5 w-5 text-slate-400" />
              <Link 
                to={auth.isAuthenticated ? '/student/resources?tab=search' : '/resources'}
                className="h-11 min-w-0 flex-1 border-0 bg-transparent text-sm text-slate-900 outline-none placeholder:text-slate-400"
              >
                搜索课程、资源、文档、视频
              </Link>
              <Link 
                to={auth.isAuthenticated ? '/student/resources?tab=search' : '/login'}
                className="inline-flex h-11 items-center gap-2 rounded-md bg-primary px-5 text-sm font-medium text-primary-foreground"
              >
                {auth.isAuthenticated ? '搜索' : '登录后搜索'}
                <ArrowRight className="h-4 w-4" />
              </Link>
            </div>
          </div>

          <div className="grid content-center gap-4 sm:grid-cols-2">
            {quickStats.map(item => {
              const Icon = item.icon;
              return (
                <div key={item.label} className="rounded-lg border border-white/20 bg-white/15 p-5 backdrop-blur">
                  <Icon className="mb-4 h-7 w-7 text-white" />
                  <div className="text-sm text-white/80">{item.label}</div>
                  <div className="mt-1 text-xl font-semibold">{item.value}</div>
                </div>
              );
            })}
          </div>
        </div>
      </section>

      <section className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
        {hasError && (
          <div className="mb-6 rounded-lg border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800">
            数据接口暂时不可用。请确认 API 服务运行在 https://localhost:44305，并已放行当前前端地址。
          </div>
        )}

        <div className="grid gap-8 lg:grid-cols-[1fr_360px]">
          <div className="space-y-8">
            <SectionHeader title="在线精品课程" description="来自课程中心的已发布课程" action="全部课程" />
            <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
              {(isLoading ? Array.from({ length: 8 }) : courses).map((course, index) => (
                <CourseCard key={isLoading ? index : (course as Course).id} course={course as Course | undefined} />
              ))}
            </div>

            <SectionHeader title="推荐教学资源" description="资源库审核通过的公开资源" action="进入资源库" />
            <div className="grid gap-4 md:grid-cols-2">
              {(isLoading ? Array.from({ length: 6 }) : resources.slice(0, 6)).map((resource, index) => (
                <ResourceCard
                  key={isLoading ? index : (resource as Resource).id}
                  resource={resource as Resource | undefined}
                />
              ))}
            </div>
          </div>

          <aside className="space-y-6">
            <div className="rounded-lg border border-slate-200 bg-white p-5">
              <SectionTitle title="学习动态" icon={Flame} />
              <div className="mt-4 space-y-4">
                {(isLoading ? Array.from({ length: 5 }) : news.slice(0, 5)).map((item, index) => (
                  <NewsRow key={isLoading ? index : (item as NewsArticle).id} item={item as NewsArticle | undefined} />
                ))}
              </div>
            </div>

            <div className="rounded-lg border border-slate-200 bg-white p-5">
              <SectionTitle title="热门资源" icon={Star} />
              <div className="mt-4 space-y-3">
                {resources.slice(0, 6).map((resource, index) => (
                  <div key={resource.id} className="flex items-center gap-3">
                    <div className="flex h-7 w-7 items-center justify-center rounded-md bg-slate-100 text-sm font-semibold text-primary">
                      {index + 1}
                    </div>
                    <div className="min-w-0 flex-1">
                      <div className="truncate text-sm font-medium text-slate-800">{resource.name}</div>
                      <div className="text-xs text-slate-500">
                        {compactNumber(resource.viewCount)} 次浏览
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </aside>
        </div>
      </section>
    </>
  );
}

function SectionHeader({ title, description, action }: { title: string; description: string; action: string }) {
  return (
    <div className="flex items-end justify-between gap-4">
      <div>
        <h2 className="text-2xl font-semibold text-slate-950">{title}</h2>
        <p className="mt-1 text-sm text-slate-500">{description}</p>
      </div>
      <button className="hidden h-9 items-center gap-1 rounded-md px-3 text-sm font-medium text-primary hover:bg-sky-50 sm:inline-flex">
        {action}
        <ArrowRight className="h-4 w-4" />
      </button>
    </div>
  );
}

function SectionTitle({ title, icon: Icon }: { title: string; icon: typeof Flame }) {
  return (
    <div className="flex items-center gap-2">
      <Icon className="h-5 w-5 text-primary" />
      <h2 className="text-lg font-semibold text-slate-950">{title}</h2>
    </div>
  );
}

function CourseCard({ course }: { course?: Course }) {
  if (!course) return <SkeletonCard />;

  return (
    <article className="group overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm transition hover:-translate-y-0.5 hover:shadow-md">
      <div className="relative aspect-[4/2.35] bg-gradient-to-br from-sky-100 via-cyan-50 to-emerald-100">
        {course.coverImageUrl ? (
          <img src={course.coverImageUrl} alt="" className="h-full w-full object-cover" />
        ) : (
          <div className="flex h-full items-center justify-center">
            <BookOpen className="h-10 w-10 text-primary/70" />
          </div>
        )}
        <span className="absolute left-3 top-3 rounded-md bg-white/90 px-2 py-1 text-xs font-medium text-primary">
          {course.major || '专业课程'}
        </span>
      </div>
      <div className="p-4">
        <h3 className="line-clamp-2 min-h-11 text-base font-semibold text-slate-950 group-hover:text-primary">
          {course.title}
        </h3>
        <p className="mt-2 line-clamp-2 min-h-10 text-sm leading-5 text-slate-500">
          {course.description || '暂无课程简介'}
        </p>
        <div className="mt-4 flex items-center justify-between text-xs text-slate-500">
          <span className="inline-flex items-center gap-1">
            <Users className="h-3.5 w-3.5" />
            {compactNumber(course.studentCount)} 人学习
          </span>
          <span>{course.semester || `${course.credits ?? 0} 学分`}</span>
        </div>
      </div>
    </article>
  );
}

function ResourceCard({ resource }: { resource?: Resource }) {
  if (!resource) return <SkeletonRow />;

  const isVideo = resource.resourceType === 1 || resource.fileExtension?.toLowerCase().includes('mp4');
  const Icon = isVideo ? PlayCircle : FileText;

  return (
    <article className="flex gap-4 rounded-lg border border-slate-200 bg-white p-4 shadow-sm hover:border-sky-200 hover:shadow-md">
      <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-md bg-sky-50 text-primary">
        <Icon className="h-6 w-6" />
      </div>
      <div className="min-w-0 flex-1">
        <h3 className="truncate text-base font-semibold text-slate-950">{resource.name}</h3>
        <p className="mt-1 line-clamp-2 text-sm leading-5 text-slate-500">
          {resource.description || resource.originalFileName || '暂无资源简介'}
        </p>
        <div className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-slate-500">
          <span>{resource.categoryName || '未分类'}</span>
          <span>{resource.fileExtension || '资源'}</span>
          <span>{compactNumber(resource.downloadCount)} 次下载</span>
        </div>
      </div>
    </article>
  );
}

function NewsRow({ item }: { item?: NewsArticle }) {
  if (!item) return <SkeletonLine />;

  return (
    <article className="border-b border-slate-100 pb-4 last:border-0 last:pb-0">
      <h3 className="line-clamp-2 text-sm font-medium leading-5 text-slate-900 hover:text-primary">{item.title}</h3>
      <div className="mt-2 flex items-center justify-between text-xs text-slate-500">
        <span>{item.categoryName || '资讯'}</span>
        <span>{formatDate(item.publishedAt || item.creationTime)}</span>
      </div>
    </article>
  );
}

function SkeletonCard() {
  return (
    <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
      <div className="aspect-[4/2.35] animate-pulse bg-slate-100" />
      <div className="space-y-3 p-4">
        <div className="h-4 animate-pulse rounded bg-slate-100" />
        <div className="h-4 w-2/3 animate-pulse rounded bg-slate-100" />
        <div className="h-3 w-1/2 animate-pulse rounded bg-slate-100" />
      </div>
    </div>
  );
}

function SkeletonRow() {
  return (
    <div className="flex gap-4 rounded-lg border border-slate-200 bg-white p-4">
      <div className="h-12 w-12 animate-pulse rounded-md bg-slate-100" />
      <div className="flex-1 space-y-3">
        <div className="h-4 animate-pulse rounded bg-slate-100" />
        <div className="h-4 w-2/3 animate-pulse rounded bg-slate-100" />
      </div>
    </div>
  );
}

function SkeletonLine() {
  return (
    <div className="space-y-2 border-b border-slate-100 pb-4">
      <div className="h-4 animate-pulse rounded bg-slate-100" />
      <div className="h-3 w-1/3 animate-pulse rounded bg-slate-100" />
    </div>
  );
}
