import { Link } from 'react-router-dom';
import { GraduationCap, BookOpen, ArrowRight } from 'lucide-react';

interface MicroMajorBrief {
  id: string;
  title: string;
  coverImageUrl?: string;
  courseCount: number;
}

interface Props {
  microMajors: MicroMajorBrief[];
  tenantId: string;
}

export function MicroMajorSection({ microMajors, tenantId }: Props) {
  return (
    <section>
      <div className="flex items-end justify-between mb-6">
        <div>
          <h2 className="text-2xl font-bold text-[#1a1a1a]">专业建设</h2>
          <p className="mt-1 text-sm text-[#999]">Professional construction</p>
        </div>
        <Link
          to={`/tenant/${tenantId}/micro-majors`}
          className="hidden sm:inline-flex items-center gap-1 text-sm text-[#0056D2] hover:text-[#0041A8] font-medium"
        >
          全部微专业 <ArrowRight className="h-4 w-4" />
        </Link>
      </div>

      <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
        {microMajors.slice(0, 3).map(mm => (
          <Link
            key={mm.id}
            to={`/tenant/${tenantId}/micro-majors/${mm.id}`}
            className="group relative overflow-hidden rounded-lg border border-[#E8E8E8] bg-white hover:border-[#0056D2] hover:shadow-md transition-all"
          >
            {/* Cover */}
            <div className="aspect-[16/9] bg-gradient-to-br from-[#0056D2]/10 via-[#1890FF]/5 to-[#52C41A]/10 flex items-center justify-center">
              {mm.coverImageUrl ? (
                <img src={mm.coverImageUrl} alt={mm.title} className="h-full w-full object-cover" />
              ) : (
                <GraduationCap className="h-14 w-14 text-[#0056D2]/30 group-hover:text-[#0056D2]/50 transition-colors" />
              )}
            </div>
            {/* Info */}
            <div className="p-4">
              <h3 className="font-semibold text-[#1a1a1a] line-clamp-2 group-hover:text-[#0056D2] transition-colors">
                {mm.title}
              </h3>
              <div className="flex items-center gap-1 mt-2 text-xs text-[#999]">
                <BookOpen className="h-3.5 w-3.5" />
                {mm.courseCount} 门课程
              </div>
            </div>
          </Link>
        ))}
      </div>
    </section>
  );
}
