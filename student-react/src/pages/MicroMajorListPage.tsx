import { useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { GraduationCap, BookOpen, Search, Users } from 'lucide-react';
import { getPublishedMicroMajors } from '../lib/api';
import type { MicroMajorDto } from '../lib/types';

export function MicroMajorListPage() {
  const { tenantId } = useParams<{ tenantId: string }>();
  const [search, setSearch] = useState('');

  const { data, isLoading } = useQuery({
    queryKey: ['micro-majors', 'published'],
    queryFn: () => getPublishedMicroMajors(0, 100),
  });

  const microMajors = data?.items || [];
  const filtered = search
    ? microMajors.filter(m => m.title.toLowerCase().includes(search.toLowerCase()))
    : microMajors;

  return (
    <div className="mx-auto max-w-[1200px] px-4 sm:px-6 lg:px-8 py-6">
      {/* Header */}
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-[#1a1a1a]">微专业</h1>
        <p className="text-sm text-[#999] mt-1">浏览全部微专业项目</p>
      </div>

      {/* Search */}
      <div className="mb-6">
        <div className="flex items-center gap-2 max-w-md bg-white rounded-lg border border-[#E8E8E8] px-3 py-2">
          <Search className="h-4 w-4 text-[#999]" />
          <input
            type="text"
            value={search}
            onChange={e => setSearch(e.target.value)}
            placeholder="搜索微专业..."
            className="flex-1 border-0 outline-none text-sm text-[#1a1a1a] placeholder:text-[#999]"
          />
        </div>
      </div>

      {/* Grid */}
      {isLoading ? (
        <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <div key={i} className="rounded-lg border border-[#E8E8E8] bg-white overflow-hidden">
              <div className="aspect-[16/9] bg-gray-100 animate-pulse" />
              <div className="p-4 space-y-2">
                <div className="h-5 w-2/3 bg-gray-100 animate-pulse rounded" />
                <div className="h-4 w-1/2 bg-gray-100 animate-pulse rounded" />
              </div>
            </div>
          ))}
        </div>
      ) : filtered.length === 0 ? (
        <div className="text-center py-16 text-[#999]">
          <GraduationCap className="h-12 w-12 mx-auto mb-3 text-[#ccc]" />
          <p className="text-sm">{search ? '未找到匹配的微专业' : '暂无微专业'}</p>
        </div>
      ) : (
        <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {filtered.map(mm => (
            <Link
              key={mm.id}
              to={`/tenant/${tenantId}/micro-majors/${mm.id}`}
              className="group rounded-lg border border-[#E8E8E8] bg-white overflow-hidden hover:border-[#0056D2] hover:shadow-md transition-all"
            >
              <div className="aspect-[16/9] bg-gradient-to-br from-[#0056D2]/10 via-[#1890FF]/5 to-[#52C41A]/10 flex items-center justify-center">
                {mm.coverImageUrl ? (
                  <img src={mm.coverImageUrl} alt="" className="h-full w-full object-cover" />
                ) : (
                  <GraduationCap className="h-12 w-12 text-[#0056D2]/30 group-hover:text-[#0056D2]/50 transition-colors" />
                )}
              </div>
              <div className="p-4">
                <h3 className="font-semibold text-[#1a1a1a] line-clamp-2 group-hover:text-[#0056D2] transition-colors">
                  {mm.title}
                </h3>
                {mm.summary && (
                  <p className="text-xs text-[#999] mt-1 line-clamp-2">{mm.summary}</p>
                )}
                <div className="flex items-center gap-4 mt-3 text-xs text-[#999]">
                  <span className="flex items-center gap-1">
                    <BookOpen className="h-3.5 w-3.5" />
                    {mm.courseCount} 门课程
                  </span>
                  <span className="flex items-center gap-1">
                    <Users className="h-3.5 w-3.5" />
                    {mm.enrollmentCount} 人报名
                  </span>
                </div>
              </div>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
