import { BookOpen, FileText, PlayCircle, Users } from 'lucide-react';

const statConfig = [
  { key: 'courseCount', label: '课程数', icon: BookOpen, color: '#0056D2' },
  { key: 'microCourseCount', label: '微课数', icon: PlayCircle, color: '#1890FF' },
  { key: 'resourceCount', label: '素材数', icon: FileText, color: '#52C41A' },
  { key: 'studentCount', label: '学员数', icon: Users, color: '#FAAD14' },
];

interface StatsSectionProps {
  stats: {
    courseCount: number;
    resourceCount: number;
    studentCount: number;
    microMajorCount: number;
  } | null;
}

export function StatsSection({ stats }: StatsSectionProps) {
  // Map to display config
  const displayStats = stats
    ? [
        { label: '课程数', value: stats.courseCount, icon: BookOpen, color: '#0056D2' },
        { label: '微专业', value: stats.microMajorCount, icon: PlayCircle, color: '#1890FF' },
        { label: '素材数', value: stats.resourceCount, icon: FileText, color: '#52C41A' },
        { label: '学员数', value: stats.studentCount, icon: Users, color: '#FAAD14' },
      ]
    : statConfig.map(s => ({ label: s.label, value: 0, icon: s.icon, color: s.color }));

  return (
    <section className="bg-white border-y border-[#E8E8E8]">
      <div className="mx-auto max-w-[1400px] px-4 sm:px-6 lg:px-8 py-10">
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-6">
          {displayStats.map(stat => {
            const Icon = stat.icon;
            return (
              <div key={stat.label} className="flex items-center gap-4 p-4 rounded-lg hover:bg-[#F5F7FA] transition-colors">
                <div
                  className="flex h-12 w-12 shrink-0 items-center justify-center rounded-lg"
                  style={{ backgroundColor: `${stat.color}15` }}
                >
                  <Icon className="h-6 w-6" style={{ color: stat.color }} />
                </div>
                <div>
                  <div className="text-2xl font-bold text-[#1a1a1a]">
                    {stats ? stat.value.toLocaleString() : '-'}
                  </div>
                  <div className="text-sm text-[#999]">{stat.label}</div>
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </section>
  );
}
