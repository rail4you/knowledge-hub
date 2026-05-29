import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Search, ArrowRight, GraduationCap, Users, Building2, Briefcase } from 'lucide-react';

const roleEntries = [
  { icon: GraduationCap, label: '学生用户', desc: '在线自学，提升专业能力和职业素养' },
  { icon: Users, label: '教师用户', desc: '整合教学资源，在线拓展教学项目课程' },
  { icon: Building2, label: '社会用户', desc: '核心技能平台，全面知识拓展和定制需求' },
  { icon: Briefcase, label: '企业用户', desc: '及时资讯，丰富的资料专业入门好帮手' },
];

const banners = [
  {
    title: '面向职业教育的数字化资源平台',
    subtitle: '汇聚精品课程、教学素材与知识图谱',
    bg: 'from-[#0b6bcb] via-[#0f8fbe] to-[#18a999]',
  },
  {
    title: 'AI 驱动的智能学习体验',
    subtitle: '知识图谱、智能推荐、个性化学习路径',
    bg: 'from-[#667eea] via-[#764ba2] to-[#f093fb]',
  },
];

export function HeroBanner() {
  const navigate = useNavigate();
  const [searchText, setSearchText] = useState('');
  const [currentBanner, setCurrentBanner] = useState(0);

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    if (searchText.trim()) {
      navigate(`/?search=${encodeURIComponent(searchText.trim())}`);
    }
  };

  // Auto-switch banners every 5 seconds
  // (simplified: just static for now with manual dots)

  const banner = banners[currentBanner];

  return (
    <section className={`bg-gradient-to-br ${banner.bg} text-white relative overflow-hidden`}>
      {/* Decorative dots */}
      <div className="absolute inset-0 opacity-10">
        <div className="absolute top-10 left-[10%] w-64 h-64 rounded-full bg-white blur-3xl" />
        <div className="absolute bottom-10 right-[10%] w-96 h-96 rounded-full bg-white blur-3xl" />
      </div>

      <div className="relative mx-auto max-w-[1400px] px-4 sm:px-6 lg:px-8 py-16 lg:py-20">
        {/* Hero content */}
        <div className="text-center max-w-3xl mx-auto mb-10">
          <h1 className="text-3xl sm:text-4xl lg:text-5xl font-bold tracking-tight leading-tight">
            {banner.title}
          </h1>
          <p className="mt-4 text-lg text-white/80">
            {banner.subtitle}
          </p>
        </div>

        {/* Search bar */}
        <form onSubmit={handleSearch} className="max-w-2xl mx-auto mb-12">
          <div className="flex items-center gap-2 bg-white rounded-lg p-2 shadow-xl shadow-black/10">
            <Search className="ml-3 h-5 w-5 text-[#999] shrink-0" />
            <input
              type="text"
              value={searchText}
              onChange={e => setSearchText(e.target.value)}
              placeholder="搜索课程、资源、文档、视频..."
              className="flex-1 h-11 border-0 bg-transparent text-sm text-[#1a1a1a] outline-none placeholder:text-[#999]"
            />
            <button
              type="submit"
              className="inline-flex h-11 items-center gap-2 rounded-md bg-[#0056D2] px-5 text-sm font-medium text-white hover:bg-[#0041A8] transition-colors shrink-0"
            >
              搜索
              <ArrowRight className="h-4 w-4" />
            </button>
          </div>
        </form>

        {/* Role entries */}
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 max-w-4xl mx-auto">
          {roleEntries.map(role => {
            const Icon = role.icon;
            return (
              <div
                key={role.label}
                className="flex flex-col items-center gap-2 p-4 rounded-lg bg-white/10 backdrop-blur border border-white/15 hover:bg-white/20 transition-colors cursor-pointer"
              >
                <Icon className="h-6 w-6 text-white/80" />
                <span className="text-sm font-semibold">{role.label}</span>
                <span className="text-xs text-white/60 text-center leading-tight">{role.desc}</span>
              </div>
            );
          })}
        </div>

        {/* Banner dots */}
        <div className="flex justify-center gap-2 mt-8">
          {banners.map((_, i) => (
            <button
              key={i}
              onClick={() => setCurrentBanner(i)}
              className={`h-2 rounded-full transition-all ${
                i === currentBanner ? 'w-8 bg-white' : 'w-2 bg-white/40'
              }`}
            />
          ))}
        </div>
      </div>
    </section>
  );
}
