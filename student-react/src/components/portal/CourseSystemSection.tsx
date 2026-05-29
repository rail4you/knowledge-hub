import { useState } from 'react';
import { Link } from 'react-router-dom';
import { BookOpen, Users, ArrowRight } from 'lucide-react';

interface CourseBrief {
  id: string;
  title: string;
  coverImageUrl?: string;
  teacherName?: string;
  studentCount: number;
}

interface Props {
  courses: CourseBrief[];
  tenantId: string;
}

const courseTabs = [
  { key: 'all', label: '全部课程' },
  { key: 'basic', label: '专业基础课' },
  { key: 'core', label: '专业核心课' },
  { key: 'extended', label: '专业拓展课' },
];

export function CourseSystemSection({ courses, tenantId }: Props) {
  const [activeTab, setActiveTab] = useState('all');

  const displayCourses = courses.slice(0, 8);

  return (
    <section>
      <div className="flex items-end justify-between mb-6">
        <div>
          <h2 className="text-2xl font-bold text-[#1a1a1a]">课程体系</h2>
          <p className="mt-1 text-sm text-[#999]">Educational course system</p>
        </div>
        <Link
          to={`/tenant/${tenantId}/courses`}
          className="hidden sm:inline-flex items-center gap-1 text-sm text-[#0056D2] hover:text-[#0041A8] font-medium"
        >
          全部课程 <ArrowRight className="h-4 w-4" />
        </Link>
      </div>

      {/* Tab filters */}
      <div className="flex gap-1 mb-6 overflow-x-auto">
        {courseTabs.map(tab => (
          <button
            key={tab.key}
            onClick={() => setActiveTab(tab.key)}
            className={`px-4 py-2 rounded-md text-sm font-medium whitespace-nowrap transition-colors ${
              activeTab === tab.key
                ? 'bg-[#0056D2] text-white'
                : 'text-[#666] hover:bg-[#F0F6FF] hover:text-[#0056D2]'
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {/* Course cards */}
      {displayCourses.length === 0 ? (
        <div className="text-center py-8 text-[#999] text-sm">暂无课程</div>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {displayCourses.map(course => (
            <Link
              key={course.id}
              to={`/tenant/${tenantId}/courses/${course.id}`}
              className="group rounded-lg border border-[#E8E8E8] bg-white overflow-hidden hover:border-[#0056D2] hover:shadow-md transition-all"
            >
              {/* Cover */}
              <div className="aspect-[16/10] bg-gradient-to-br from-[#f0f4ff] to-[#e8f0fe] flex items-center justify-center">
                {course.coverImageUrl ? (
                  <img src={course.coverImageUrl} alt="" className="h-full w-full object-cover" />
                ) : (
                  <BookOpen className="h-10 w-10 text-[#0056D2]/40 group-hover:text-[#0056D2]/60" />
                )}
              </div>
              {/* Info */}
              <div className="p-3">
                <h4 className="text-sm font-semibold text-[#1a1a1a] line-clamp-2 group-hover:text-[#0056D2] transition-colors">
                  {course.title}
                </h4>
                <div className="flex items-center justify-between mt-2 text-xs text-[#999]">
                  {course.teacherName && (
                    <span>主持教师：{course.teacherName}</span>
                  )}
                  <span className="flex items-center gap-1">
                    <Users className="h-3 w-3" />
                    {course.studentCount}
                  </span>
                </div>
              </div>
            </Link>
          ))}
        </div>
      )}
    </section>
  );
}
