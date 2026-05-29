import { useQuery } from '@tanstack/react-query';
import { useParams, Link } from 'react-router-dom';
import { api } from '../lib/api';
import { BookOpen, Download, Users, GraduationCap, ArrowRight, Search } from 'lucide-react';
import { StatsSection } from '../components/home/StatsSection';
import { MicroMajorSection } from '../components/portal/MicroMajorSection';
import { CourseSystemSection } from '../components/portal/CourseSystemSection';
import { MaterialCenterSection } from '../components/portal/MaterialCenterSection';
import { PartnerSection } from '../components/portal/PartnerSection';
import { MiniKnowledgeGraph } from '../components/portal/MiniKnowledgeGraph';

interface PortalHomeData {
  tenantInfo: { id: string; name: string; description?: string; logoUrl?: string; industryField?: string };
  stats: { courseCount: number; resourceCount: number; studentCount: number; microMajorCount: number };
  microMajors: { id: string; title: string; coverImageUrl?: string; courseCount: number }[];
  featuredCourses: { id: string; title: string; coverImageUrl?: string; teacherName?: string; studentCount: number }[];
  latestMaterials: { id: string; name: string; fileExtension?: string; downloadCount: number; coverUrl?: string }[];
  partners?: { id: string; name: string }[];
  latestNews: { id: string; title: string; publishedAt?: string }[];
}

export function TenantPortalPage() {
  const { tenantId } = useParams<{ tenantId: string }>();

  const { data, isLoading, error } = useQuery<PortalHomeData>({
    queryKey: ['portal', 'home-data', tenantId],
    queryFn: async () => {
      const { data } = await api.get<PortalHomeData>(`/api/app/portal/home-data/${tenantId}`);
      return data;
    },
    enabled: !!tenantId,
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <div className="animate-spin rounded-full h-8 w-8 border-3 border-[#E8E8E8] border-t-[#0056D2]" />
      </div>
    );
  }

  if (error || !data) {
    return (
      <div className="text-center py-16 text-[#999]">
        <GraduationCap className="h-12 w-12 mx-auto mb-3 text-[#ccc]" />
        <p className="text-sm">加载失败，请稍后重试</p>
      </div>
    );
  }

  return (
    <>
      {/* Hero / Intro Section */}
      <IntroSection
        description={data.tenantInfo.description}
        industryField={data.tenantInfo.industryField}
      />
      <StatsSection stats={data.stats} />

      <div className="mx-auto max-w-[1400px] px-4 sm:px-6 lg:px-8 space-y-16 py-8">
        {/* MicroMajors */}
        {data.microMajors.length > 0 && (
          <MicroMajorSection microMajors={data.microMajors} tenantId={tenantId!} />
        )}

        {/* Course System */}
        {data.featuredCourses.length > 0 && (
          <CourseSystemSection courses={data.featuredCourses} tenantId={tenantId!} />
        )}

        {/* Materials */}
        {data.latestMaterials.length > 0 && (
          <MaterialCenterSection materials={data.latestMaterials} tenantId={tenantId!} />
        )}

        {/* Mini Knowledge Graph */}
        <MiniKnowledgeGraph tenantId={tenantId!} />

        {/* Partners */}
        {data.partners && data.partners.length > 0 && (
          <PartnerSection partners={data.partners} />
        )}
      </div>
    </>
  );
}

function IntroSection({ description, industryField }: { description?: string; industryField?: string }) {
  return (
    <section className="bg-white border-b border-[#E8E8E8]">
      <div className="mx-auto max-w-[1400px] px-4 sm:px-6 lg:px-8 py-12">
        <div className="max-w-3xl">
          <div className="flex items-center gap-3 mb-4">
            {industryField && (
              <span className="inline-flex items-center px-3 py-1 rounded-full bg-[#E8F0FE] text-[#0056D2] text-xs font-medium">
                {industryField}
              </span>
            )}
            <span className="text-sm text-[#999] uppercase tracking-wide">Introduction to Resource Library</span>
          </div>
          <h2 className="text-2xl font-bold text-[#1a1a1a] mb-4">资源库简介</h2>
          <p className="text-sm text-[#666] leading-relaxed">
            {description || '暂无简介'}
          </p>
        </div>
      </div>
    </section>
  );
}
