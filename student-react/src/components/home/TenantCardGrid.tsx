import { useState } from 'react';
import { Link } from 'react-router-dom';
import { GraduationCap, BookOpen, FileText } from 'lucide-react';

interface TenantWithStats {
  id: string;
  name: string;
  courseCount: number;
  resourceCount: number;
  studentCount: number;
}

const sourceFilters = [
  { key: 'all', label: '全部' },
  { key: 'national', label: '国家级' },
  { key: 'provincial', label: '省级' },
  { key: 'school', label: '校级' },
  { key: 'enterprise', label: '企业' },
];

interface TenantCardGridProps {
  tenants: TenantWithStats[];
  isLoading: boolean;
}

export function TenantCardGrid({ tenants, isLoading }: TenantCardGridProps) {
  const [activeFilter, setActiveFilter] = useState('all');

  const filteredTenants = tenants; // Simplified: no source field yet

  return (
    <section className="mx-auto max-w-[1400px] px-4 sm:px-6 lg:px-8 py-16">
      {/* Section header */}
      <div className="mb-8">
        <h2 className="text-2xl font-bold text-[#1a1a1a]">资源库</h2>
        <p className="mt-1 text-sm text-[#999]">浏览全部资源库项目，按来源筛选</p>
      </div>

      {/* Filter tabs */}
      <div className="flex gap-1 mb-6 overflow-x-auto pb-1">
        {sourceFilters.map(f => (
          <button
            key={f.key}
            onClick={() => setActiveFilter(f.key)}
            className={`px-5 py-2 rounded-md text-sm font-medium whitespace-nowrap transition-colors ${
              activeFilter === f.key
                ? 'bg-[#0056D2] text-white'
                : 'text-[#666] hover:bg-[#F0F6FF] hover:text-[#0056D2]'
            }`}
          >
            {f.label}
          </button>
        ))}
      </div>

      {/* Card grid */}
      {isLoading ? (
        <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {Array.from({ length: 8 }).map((_, i) => (
            <div key={i} className="rounded-lg border border-[#E8E8E8] bg-white overflow-hidden">
              <div className="aspect-[16/10] bg-gray-100 animate-pulse" />
              <div className="p-4 space-y-2">
                <div className="h-5 w-2/3 bg-gray-100 animate-pulse rounded" />
                <div className="h-4 w-full bg-gray-100 animate-pulse rounded" />
                <div className="h-3 w-1/2 bg-gray-100 animate-pulse rounded" />
              </div>
            </div>
          ))}
        </div>
      ) : filteredTenants.length === 0 ? (
        <div className="text-center py-16 text-[#999]">
          <GraduationCap className="h-12 w-12 mx-auto mb-3 text-[#ccc]" />
          <p className="text-sm">暂无资源库项目</p>
        </div>
      ) : (
        <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {filteredTenants.map(tenant => (
            <Link
              key={tenant.id}
              to={`/tenant/${tenant.id}`}
              className="group rounded-lg border border-[#E8E8E8] bg-white overflow-hidden hover:border-[#0056D2] hover:shadow-md transition-all hover:-translate-y-0.5"
            >
              {/* Cover */}
              <div className="aspect-[16/10] bg-gradient-to-br from-[#0056D2]/10 via-[#1890FF]/5 to-[#52C41A]/10 flex items-center justify-center">
                <GraduationCap className="h-12 w-12 text-[#0056D2]/40 group-hover:text-[#0056D2]/60 transition-colors" />
              </div>

              {/* Info */}
              <div className="p-4">
                <h3 className="font-semibold text-[#1a1a1a] line-clamp-2 mb-1 group-hover:text-[#0056D2] transition-colors">
                  {tenant.name}
                </h3>
                <div className="flex items-center gap-4 mt-3 text-xs text-[#999]">
                  <span className="flex items-center gap-1">
                    <BookOpen className="h-3.5 w-3.5" />
                    {tenant.courseCount} 课程
                  </span>
                  <span className="flex items-center gap-1">
                    <FileText className="h-3.5 w-3.5" />
                    {tenant.resourceCount} 素材
                  </span>
                </div>
              </div>
            </Link>
          ))}
        </div>
      )}

      {/* Load more placeholder */}
      {filteredTenants.length > 0 && (
        <div className="text-center mt-8">
          <button className="px-6 py-2.5 rounded-md border border-[#E8E8E8] text-sm text-[#666] hover:border-[#0056D2] hover:text-[#0056D2] transition-colors">
            加载更多
          </button>
        </div>
      )}
    </section>
  );
}
