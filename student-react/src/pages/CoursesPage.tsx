import { useState, useEffect } from 'react';
import { useParams, Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { BookOpen, PlayCircle, Clock, Users, Search, CheckCircle2, Eye, ChevronRight } from 'lucide-react';
import { useAuth } from '../lib/auth';
import { api } from '../lib/api';
import type { Course } from '../lib/types';

const difficultyLabels: Record<number, string> = { 1: '入门', 2: '初级', 3: '中级', 4: '高级', 5: '专家' };
const difficultyColors: Record<number, string> = {
  1: '#52c41a', 2: '#73d13d', 3: '#faad14', 4: '#fa8c16', 5: '#f5222d',
};

export function CoursesPage() {
  const { tenantId } = useParams<{ tenantId: string }>();
  const auth = useAuth();
  const [search, setSearch] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');

  useEffect(() => {
    // Don't set global tenant header - courses are tenant-scoped through the API itself
  }, [tenantId]);
  useEffect(() => { const t = setTimeout(() => setDebouncedSearch(search), 300); return () => clearTimeout(t); }, [search]);

  // Published courses for tenant
  // Published courses (no tenant header needed - API returns all published)
  const { data: published, isLoading } = useQuery<{ items: Course[]; totalCount: number }>({
    queryKey: ['courses', 'published', debouncedSearch],
    queryFn: async () => {
      const params: any = { skipCount: 0, maxResultCount: 100 };
      if (debouncedSearch) params.filter = debouncedSearch;
      const { data } = await api.get('/api/app/course/published', { params });
      return data;
    },
  });

  // My courses (only if logged in, auth token auto-attached)
  const { data: myCourses } = useQuery<{ items: Course[] }>({
    queryKey: ['courses', 'my'],
    queryFn: async () => {
      const { data } = await api.get('/api/app/course/my-courses', { params: { maxResultCount: 100, skipCount: 0 } });
      return data;
    },
    enabled: auth.isAuthenticated,
  });

  const courses = published?.items || [];
  const enrolledIds = new Set((myCourses?.items || []).map(c => c.id));
  const enrolledMap = new Map((myCourses?.items || []).map(c => [c.id, c]));
  const totalCount = published?.totalCount || 0;

  return (
    <div className="mx-auto max-w-[1400px] px-4 sm:px-6 lg:px-8 py-6">
      {/* Header */}
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-[#1a1a1a]">课程库</h1>
        <p className="text-sm text-[#999] mt-1">
          {totalCount > 0 ? `共 ${totalCount} 门课程` : '浏览全部已发布课程'}
        </p>
      </div>

      {/* Search */}
      <div className="flex items-center gap-2 max-w-lg bg-white rounded-lg border border-[#E8E8E8] px-3 py-2 mb-6">
        <Search className="h-4 w-4 text-[#999] shrink-0" />
        <input type="text" value={search} onChange={e => setSearch(e.target.value)}
          placeholder="搜索课程名称..." className="flex-1 border-0 outline-none text-sm" />
      </div>

      {/* Loading */}
      {isLoading && (
        <div className="flex justify-center min-h-[300px] items-center">
          <div className="animate-spin rounded-full h-8 w-8 border-3 border-[#E8E8E8] border-t-[#0056D2]" />
        </div>
      )}

      {/* Empty */}
      {!isLoading && courses.length === 0 && (
        <div className="text-center py-16 text-[#999] bg-white rounded-lg border border-[#E8E8E8]">
          <BookOpen className="h-12 w-12 mx-auto mb-3 text-[#ccc]" />
          <p className="text-sm">{debouncedSearch ? '未找到匹配的课程' : '暂无已发布课程'}</p>
        </div>
      )}

      {/* Grid */}
      {!isLoading && courses.length > 0 && (
        <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {courses.map(course => {
            const isEnrolled = enrolledIds.has(course.id);
            const enrolledData = enrolledMap.get(course.id);

            return (
              <div key={course.id} className="group rounded-lg border border-[#E8E8E8] bg-white overflow-hidden hover:border-[#0056D2] hover:shadow-md transition-all">
                {/* Cover */}
                <div className="aspect-[16/10] relative bg-gradient-to-br from-[#f0f4ff] to-[#e8f0fe] flex items-center justify-center">
                  {course.coverImageUrl ? (
                    <img src={course.coverImageUrl} alt="" className="h-full w-full object-cover" />
                  ) : (
                    <BookOpen className="h-12 w-12 text-[#0056D2]/30 group-hover:text-[#0056D2]/50 transition-colors" />
                  )}

                  {/* Difficulty badge */}
                  <span className="absolute top-2 left-2 px-2 py-0.5 rounded text-[10px] font-bold text-white"
                    style={{ background: difficultyColors[course.difficulty] || '#999' }}>
                    {difficultyLabels[course.difficulty] || '未知'}
                  </span>

                  {/* Enrolled badge */}
                  {isEnrolled && (
                    <span className="absolute top-2 right-2 flex items-center gap-1 px-2 py-0.5 rounded text-[10px] font-medium bg-[#F6FFED] text-[#52C41A] border border-[#B7EB8F]">
                      <CheckCircle2 className="h-3 w-3" />已选课
                    </span>
                  )}
                </div>

                {/* Body */}
                <div className="p-4">
                  <Link to={`/tenant/${tenantId}/courses/${course.id}`}
                    className="text-base font-semibold text-[#1a1a1a] line-clamp-2 hover:text-[#0056D2] transition-colors">
                    {course.title}
                  </Link>

                  {course.description && (
                    <p className="text-xs text-[#999] mt-1.5 line-clamp-2">{course.description}</p>
                  )}

                  {/* Meta */}
                  <div className="flex flex-wrap items-center gap-x-4 gap-y-1 mt-3 text-xs text-[#999]">
                    {course.teacherName && <span className="flex items-center gap-1"><Users className="h-3 w-3" />{course.teacherName}</span>}
                    {course.studentCount != null && <span className="flex items-center gap-1"><Users className="h-3 w-3" />{course.studentCount} 人</span>}
                    {course.semester && <span className="flex items-center gap-1"><Clock className="h-3 w-3" />{course.semester}</span>}
                    {course.credits && <span>{course.credits} 学分</span>}
                  </div>

                  {/* Progress bar (enrolled only) */}
                  {isEnrolled && enrolledData?.progress != null && (
                    <div className="mt-3">
                      <div className="flex items-center justify-between text-xs mb-1">
                        <span className="text-[#999]">学习进度</span>
                        <span className="font-medium text-[#0056D2]">{enrolledData.progress}%</span>
                      </div>
                      <div className="h-2 bg-[#F0F0F0] rounded-full overflow-hidden">
                        <div className="h-full bg-[#0056D2] rounded-full transition-all" style={{ width: `${enrolledData.progress}%` }} />
                      </div>
                    </div>
                  )}

                  {/* Action */}
                  <div className="mt-4 pt-3 border-t border-[#F0F0F0]">
                    <Link
                      to={`/tenant/${tenantId}/courses/${course.id}`}
                      className={`flex items-center justify-center gap-1.5 py-2 rounded-md text-sm font-medium transition-colors ${
                        isEnrolled
                          ? 'bg-[#0056D2] text-white hover:bg-[#0041A8]'
                          : 'bg-[#F5F7FA] text-[#666] hover:bg-[#E8F0FE] hover:text-[#0056D2]'
                      }`}
                    >
                      {isEnrolled ? (
                        <><PlayCircle className="h-4 w-4" />继续学习</>
                      ) : (
                        <><Eye className="h-4 w-4" />预览课程</>
                      )}
                    </Link>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
