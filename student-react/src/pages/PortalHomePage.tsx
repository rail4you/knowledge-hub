import { useQuery } from '@tanstack/react-query';
import { api } from '../lib/api';
import { HeroBanner } from '../components/home/HeroBanner';
import { StatsSection } from '../components/home/StatsSection';
import { TenantCardGrid } from '../components/home/TenantCardGrid';

interface TenantWithStats {
  id: string;
  name: string;
  courseCount: number;
  resourceCount: number;
  studentCount: number;
}

export function PortalHomePage() {
  const { data: tenants, isLoading } = useQuery<TenantWithStats[]>({
    queryKey: ['public', 'tenants-with-stats'],
    queryFn: async () => {
      const { data } = await api.get<TenantWithStats[]>('/api/public/tenants-with-stats');
      return data;
    },
  });

  // Aggregate stats from all tenants
  const totalStats = tenants
    ? tenants.reduce(
        (acc, t) => ({
          courseCount: acc.courseCount + t.courseCount,
          resourceCount: acc.resourceCount + t.resourceCount,
          studentCount: acc.studentCount + t.studentCount,
          microMajorCount: 0,
        }),
        { courseCount: 0, resourceCount: 0, studentCount: 0, microMajorCount: 0 }
      )
    : null;

  return (
    <>
      <HeroBanner />
      <StatsSection stats={totalStats} />
      <TenantCardGrid tenants={tenants || []} isLoading={isLoading} />
    </>
  );
}
