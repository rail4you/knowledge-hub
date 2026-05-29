import { useState, useEffect, useCallback } from 'react';
import { useParams, Link } from 'react-router-dom';
import { useQuery, useQueries } from '@tanstack/react-query';
import {
  ArrowLeft, BookOpen, Users, Clock, Play,
  FileText, BarChart3, Network, Inbox,
  ChevronRight, ChevronDown,
} from 'lucide-react';
import { useAuth } from '../lib/auth';
import {
  api, setTenantId,
  getCourseDetail, getCourseExercises, getChapterProgress,
  getCourseKnowledgeResources, submitExerciseAnswer, getKnowledgeGraph,
} from '../lib/api';
import type { CourseDetail, ChapterDto, ExerciseDto, ChapterProgressDto, KnowledgeResourceDto } from '../lib/types';
import { ExerciseList } from '../components/course/ExerciseList';
import { VideoPlayer } from '../components/course/VideoPlayer';
import { KnowledgeGraph } from '../components/knowledge-graph/KnowledgeGraph';

const difficultyLabels: Record<number, string> = { 1: '入门', 2: '初级', 3: '中级', 4: '高级', 5: '专家' };
const difficultyColors: Record<number, string> = {
  1: '#52c41a', 2: '#73d13d', 3: '#faad14', 4: '#fa8c16', 5: '#f5222d',
};

type TabKey = 'info' | 'chapters' | 'exercises' | 'materials' | 'graph' | 'stats';

const tabs: { key: TabKey; label: string; icon: typeof BookOpen }[] = [
  { key: 'info', label: '课程信息', icon: BookOpen },
  { key: 'chapters', label: '章节学习', icon: FileText },
  { key: 'exercises', label: '习题练习', icon: Play },
  { key: 'materials', label: '教学素材', icon: FileText },
  { key: 'graph', label: '知识图谱', icon: Network },
  { key: 'stats', label: '学习统计', icon: BarChart3 },
];

export function CourseDetailPage() {
  const { tenantId, courseId } = useParams<{ tenantId: string; courseId: string }>();
  const auth = useAuth();
  const [activeTab, setActiveTab] = useState<TabKey>('info');
  const [expandedChapters, setExpandedChapters] = useState<Set<string>>(new Set());
  const [selectedChapterId, setSelectedChapterId] = useState<string | undefined>();
  const [answers, setAnswers] = useState<Map<string, { answer: string; isCorrect: boolean | null }>>(new Map());

  // Set tenant context
  useEffect(() => {
    if (tenantId) setTenantId(tenantId);
  }, [tenantId]);

  // Queries
  const { data: course, isLoading } = useQuery({
    queryKey: ['course', 'detail', courseId],
    queryFn: () => getCourseDetail(courseId!),
    enabled: !!courseId,
  });

  const { data: exercises } = useQuery({
    queryKey: ['exercises', courseId, selectedChapterId],
    queryFn: () => getCourseExercises(courseId!, selectedChapterId),
    enabled: !!courseId && activeTab === 'exercises',
  });

  const { data: chapterProgress } = useQuery({
    queryKey: ['chapter-progress', courseId],
    queryFn: () => getChapterProgress(courseId!),
    enabled: !!courseId && auth.isAuthenticated,
  });

  const { data: knowledgeResources } = useQuery({
    queryKey: ['knowledge-resources', courseId],
    queryFn: () => getCourseKnowledgeResources(courseId!),
    enabled: !!courseId && activeTab === 'materials',
  });

  const handleAnswer = useCallback(async (exerciseId: string, answer: string) => {
    if (!auth.isAuthenticated) {
      alert('请先登录后再答题');
      return;
    }
    try {
      const result = await submitExerciseAnswer({
        courseId: courseId!,
        chapterId: selectedChapterId,
        exerciseId,
        studentAnswer: answer,
        timeSpentTicks: 0,
      });
      setAnswers(prev => {
        const next = new Map(prev);
        next.set(exerciseId, { answer, isCorrect: result?.isCorrect ?? null });
        return next;
      });
    } catch {
      setAnswers(prev => {
        const next = new Map(prev);
        next.set(exerciseId, { answer, isCorrect: null });
        return next;
      });
    }
  }, [courseId, selectedChapterId, auth.isAuthenticated]);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <div className="animate-spin rounded-full h-8 w-8 border-3 border-[#E8E8E8] border-t-[#0056D2]" />
      </div>
    );
  }

  if (!course) {
    return (
      <div className="text-center py-16 text-[#999]">
        <Inbox className="h-12 w-12 mx-auto mb-3 text-[#ccc]" />
        <p className="text-sm">课程不存在</p>
      </div>
    );
  }

  const chapters = course.chapters || [];
  const progressMap = new Map((chapterProgress || []).map(p => [p.chapterId, p]));

  return (
    <div className="mx-auto max-w-[1200px] px-4 sm:px-6 lg:px-8 py-6">
      {/* Back */}
      <Link
        to={tenantId ? `/tenant/${tenantId}/courses` : '/student/courses'}
        className="inline-flex items-center gap-1 text-sm text-[#666] hover:text-[#0056D2] mb-4 transition-colors"
      >
        <ArrowLeft className="h-4 w-4" /> 返回课程列表
      </Link>

      {/* Course Header */}
      <div className="rounded-lg border border-[#E8E8E8] bg-white overflow-hidden mb-6">
        <div className="flex flex-col md:flex-row">
          {/* Cover */}
          <div
            className="md:w-80 h-48 md:h-auto flex items-center justify-center shrink-0"
            style={{
              background: course.coverImageUrl
                ? `url(${course.coverImageUrl}) center/cover`
                : 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
            }}
          >
            {!course.coverImageUrl && <BookOpen className="h-16 w-16 text-white/60" />}
          </div>

          {/* Info */}
          <div className="flex-1 p-6">
            <h1 className="text-xl font-bold text-[#1a1a1a] mb-2">{course.title}</h1>
            {course.description && (
              <p className="text-sm text-[#666] mb-3 line-clamp-2">{course.description}</p>
            )}

            <div className="flex flex-wrap gap-2 mb-3">
              {course.difficulty && (
                <span className="px-2 py-0.5 rounded text-xs text-white" style={{ background: difficultyColors[course.difficulty] }}>
                  {difficultyLabels[course.difficulty]}
                </span>
              )}
              {course.major && <span className="px-2 py-0.5 rounded text-xs bg-[#E8F0FE] text-[#0056D2]">{course.major}</span>}
              {course.credits && <span className="px-2 py-0.5 rounded text-xs bg-[#F0F0FF] text-[#722ED1]">{course.credits} 学分</span>}
            </div>

            <div className="flex flex-wrap gap-x-6 gap-y-1 text-xs text-[#999]">
              {course.teacherName && <span><Users className="inline h-3.5 w-3.5 mr-1" />{course.teacherName}</span>}
              {course.studentCount != null && <span><Users className="inline h-3.5 w-3.5 mr-1" />{course.studentCount} 人学习</span>}
              {course.semester && <span><Clock className="inline h-3.5 w-3.5 mr-1" />{course.semester}</span>}
            </div>

            {/* Progress */}
            {course.isEnrolled && course.progress != null && (
              <div className="mt-3 flex items-center gap-2">
                <span className="text-xs text-[#999]">学习进度</span>
                <div className="flex-1 h-2 bg-[#F0F0F0] rounded-full overflow-hidden">
                  <div className="h-full bg-[#0056D2] rounded-full transition-all" style={{ width: `${course.progress}%` }} />
                </div>
                <span className="text-xs font-medium text-[#0056D2]">{course.progress}%</span>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Tabs */}
      <div className="bg-white rounded-lg border border-[#E8E8E8] overflow-hidden">
        {/* Tab bar */}
        <div className="flex overflow-x-auto border-b border-[#E8E8E8] bg-[#F5F7FA]">
          {tabs.map(tab => {
            const Icon = tab.icon;
            return (
              <button
                key={tab.key}
                onClick={() => setActiveTab(tab.key)}
                className={`flex items-center gap-1.5 px-5 py-3 text-sm font-medium whitespace-nowrap transition-colors border-b-2 -mb-[1px] ${
                  activeTab === tab.key
                    ? 'text-[#0056D2] border-[#0056D2] bg-white'
                    : 'text-[#666] border-transparent hover:text-[#0056D2] hover:bg-[#F0F6FF]'
                }`}
              >
                <Icon className="h-4 w-4" />
                {tab.label}
              </button>
            );
          })}
        </div>

        {/* Tab content */}
        <div className="p-6 min-h-[400px]">
          {activeTab === 'info' && <InfoTab course={course} />}
          {activeTab === 'chapters' && (
            <ChaptersTab
              chapters={chapters}
              progressMap={progressMap}
              expandedChapters={expandedChapters}
              setExpandedChapters={setExpandedChapters}
              tenantId={tenantId}
              courseId={courseId!}
            />
          )}
          {activeTab === 'exercises' && (
            <ExercisesTab
              chapters={chapters}
              exercises={exercises || []}
              selectedChapterId={selectedChapterId}
              setSelectedChapterId={setSelectedChapterId}
              onAnswer={handleAnswer}
              answers={answers}
              isAuthenticated={auth.isAuthenticated}
            />
          )}
          {activeTab === 'materials' && (
            <MaterialsTab resources={knowledgeResources || []} isLoading={activeTab === 'materials'} />
          )}
          {activeTab === 'graph' && (
            <GraphTab courseId={courseId!} />
          )}
          {activeTab === 'stats' && (
            <StatsTab progress={chapterProgress || []} />
          )}
        </div>
      </div>
    </div>
  );
}

/* ======= TAB COMPONENTS ======= */

function InfoTab({ course }: { course: CourseDetail }) {
  return (
    <div className="max-w-3xl">
      <h2 className="text-lg font-semibold text-[#1a1a1a] mb-4">课程信息</h2>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <InfoRow label="课程名称" value={course.title} />
        <InfoRow label="所属专业" value={course.major || '-'} />
        <InfoRow label="授课教师" value={course.teacherName || '-'} />
        <InfoRow label="学期" value={course.semester || '-'} />
        <InfoRow label="学分" value={course.credits?.toString() || '-'} />
        <InfoRow label="学时" value={course.semesterHours?.toString() || '-'} />
        <InfoRow label="难度" value={difficultyLabels[course.difficulty || 1]} />
        <InfoRow label="学习人数" value={course.studentCount?.toString() || '0'} />
      </div>
      {course.description && (
        <div className="mt-4">
          <h3 className="text-sm font-semibold text-[#1a1a1a] mb-2">课程简介</h3>
          <p className="text-sm text-[#666] leading-relaxed">{course.description}</p>
        </div>
      )}
    </div>
  );
}

function InfoRow({ label, value }: { label: string; value?: string }) {
  return (
    <div>
      <div className="text-xs text-[#999] mb-1">{label}</div>
      <div className="text-sm text-[#1a1a1a] font-medium">{value || '-'}</div>
    </div>
  );
}

function ChaptersTab({
  chapters, progressMap, expandedChapters, setExpandedChapters, tenantId, courseId,
}: {
  chapters: ChapterDto[];
  progressMap: Map<string | undefined, ChapterProgressDto>;
  expandedChapters: Set<string>;
  setExpandedChapters: (s: Set<string>) => void;
  tenantId?: string;
  courseId: string;
}) {
  if (chapters.length === 0) {
    return (
      <div className="text-center py-12 text-[#999]">
        <FileText className="h-10 w-10 mx-auto mb-2 text-[#ccc]" />
        <p className="text-sm">暂无章节</p>
      </div>
    );
  }

  return (
    <div className="space-y-1">
      {chapters.map(ch => (
        <ChapterNode
          key={ch.id}
          chapter={ch}
          depth={0}
          progressMap={progressMap}
          expanded={expandedChapters}
          onToggle={(id) => {
            const next = new Set(expandedChapters);
            next.has(id) ? next.delete(id) : next.add(id);
            setExpandedChapters(next);
          }}
        />
      ))}
    </div>
  );
}

function ChapterNode({
  chapter, depth, progressMap, expanded, onToggle,
}: {
  chapter: ChapterDto;
  depth: number;
  progressMap: Map<string | undefined, ChapterProgressDto>;
  expanded: Set<string>;
  onToggle: (id: string) => void;
}) {
  const hasChildren = chapter.children && chapter.children.length > 0;
  const isExpanded = expanded.has(chapter.id);
  const progress = progressMap.get(chapter.id);

  return (
    <div>
      <div
        className="flex items-center gap-2 px-3 py-2.5 rounded-lg hover:bg-[#F5F7FA] transition-colors cursor-pointer"
        style={{ paddingLeft: depth * 20 + 12 }}
        onClick={() => hasChildren && onToggle(chapter.id)}
      >
        {hasChildren ? (
          isExpanded ? <ChevronDown className="h-4 w-4 text-[#999] shrink-0" /> : <ChevronRight className="h-4 w-4 text-[#999] shrink-0" />
        ) : (
          <FileText className="h-4 w-4 text-[#ccc] shrink-0" />
        )}
        <span className="text-sm text-[#1a1a1a] flex-1">{chapter.title}</span>
        {progress && (
          <span className="text-xs text-[#999] shrink-0">
            {progress.completedCount}/{progress.totalCount} 题
          </span>
        )}
      </div>
      {hasChildren && isExpanded && chapter.children!.map(child => (
        <ChapterNode
          key={child.id}
          chapter={child}
          depth={depth + 1}
          progressMap={progressMap}
          expanded={expanded}
          onToggle={onToggle}
        />
      ))}
    </div>
  );
}

function ExercisesTab({
  chapters, exercises, selectedChapterId, setSelectedChapterId, onAnswer, answers, isAuthenticated,
}: {
  chapters: ChapterDto[];
  exercises: ExerciseDto[];
  selectedChapterId?: string;
  setSelectedChapterId: (id?: string) => void;
  onAnswer: (exerciseId: string, answer: string) => void;
  answers: Map<string, { answer: string; isCorrect: boolean | null }>;
  isAuthenticated: boolean;
}) {
  // Build a flat map of chapterId -> title
  const chapterMap = new Map<string, string>();
  const walk = (ch: ChapterDto[]) => {
    for (const c of ch) {
      if (c.id) chapterMap.set(c.id, c.title || '未命名章节');
      if (c.children) walk(c.children);
    }
  };
  walk(chapters);

  const filteredExercises = selectedChapterId
    ? exercises.filter(e => e.chapterId === selectedChapterId)
    : exercises;

  return (
    <div>
      {!isAuthenticated && (
        <div className="mb-4 p-3 rounded-lg bg-[#FFFBE6] border border-[#FFE58F] text-sm text-[#8c6900]">
          请登录后答题，当前只能查看习题内容。
        </div>
      )}

      {/* Chapter filter */}
      {chapterMap.size > 0 && (
        <div className="flex flex-wrap gap-2 mb-4">
          <button
            onClick={() => setSelectedChapterId(undefined)}
            className={`px-3 py-1.5 rounded-md text-xs font-medium transition-colors ${
              !selectedChapterId ? 'bg-[#0056D2] text-white' : 'bg-[#F5F7FA] text-[#666] hover:bg-[#F0F6FF]'
            }`}
          >
            全部章节
          </button>
          {Array.from(chapterMap.entries()).map(([id, title]) => (
            <button
              key={id}
              onClick={() => setSelectedChapterId(id)}
              className={`px-3 py-1.5 rounded-md text-xs font-medium transition-colors ${
                selectedChapterId === id ? 'bg-[#0056D2] text-white' : 'bg-[#F5F7FA] text-[#666] hover:bg-[#F0F6FF]'
              }`}
            >
              {title}
            </button>
          ))}
        </div>
      )}

      <ExerciseList
        exercises={filteredExercises}
        chapterTitle={selectedChapterId ? chapterMap.get(selectedChapterId) : undefined}
        onAnswer={onAnswer}
        answeredMap={answers}
      />
    </div>
  );
}

function MaterialsTab({ resources, isLoading }: { resources: KnowledgeResourceDto[]; isLoading: boolean }) {
  if (resources.length === 0) {
    return (
      <div className="text-center py-12 text-[#999]">
        <FileText className="h-10 w-10 mx-auto mb-2 text-[#ccc]" />
        <p className="text-sm">暂无教学素材</p>
      </div>
    );
  }

  const videoResources = resources.filter(r => r.tags?.toLowerCase().includes('video'));
  const docResources = resources.filter(r => !r.tags?.toLowerCase().includes('video'));

  return (
    <div className="space-y-6">
      {/* Videos */}
      {videoResources.length > 0 && (
        <div>
          <h3 className="text-sm font-semibold text-[#1a1a1a] mb-3">视频素材</h3>
          <div className="grid gap-4 sm:grid-cols-2">
            {videoResources.map(r => (
              <VideoPlayer key={r.id} src={r.content || ''} title={r.name} />
            ))}
          </div>
        </div>
      )}

      {/* Documents */}
      {docResources.length > 0 && (
        <div>
          <h3 className="text-sm font-semibold text-[#1a1a1a] mb-3">文档素材</h3>
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {docResources.map(r => (
              <div key={r.id} className="flex items-start gap-3 p-3 rounded-lg border border-[#E8E8E8] bg-white hover:border-[#0056D2] transition-colors">
                <div className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-lg ${
                  r.importanceLevel === 'high' ? 'bg-[#FF4D4F]/10 text-[#FF4D4F]' :
                  r.importanceLevel === 'normal' ? 'bg-[#0056D2]/10 text-[#0056D2]' :
                  'bg-gray-100 text-[#999]'
                }`}>
                  <FileText className="h-5 w-5" />
                </div>
                <div className="min-w-0 flex-1">
                  <div className="text-sm font-medium text-[#1a1a1a] truncate">{r.name}</div>
                  {r.description && <div className="text-xs text-[#999] mt-0.5 line-clamp-2">{r.description}</div>}
                  {r.tags && <div className="text-xs text-[#0056D2] mt-1">{r.tags}</div>}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

function GraphTab({ courseId }: { courseId: string }) {
  const { data, isLoading } = useQuery({
    queryKey: ['knowledge-graph', 'course', courseId],
    queryFn: () => getKnowledgeGraph(courseId),
    enabled: !!courseId,
  });

  const graphData = data ? {
    nodes: data.nodes || [],
    relations: data.relations || [],
  } : null;

  return (
    <KnowledgeGraph mode="course" data={graphData} isLoading={isLoading} />
  );
}

function StatsTab({ progress }: { progress: ChapterProgressDto[] }) {
  if (progress.length === 0) {
    return (
      <div className="text-center py-12 text-[#999]">
        <BarChart3 className="h-10 w-10 mx-auto mb-2 text-[#ccc]" />
        <p className="text-sm">暂无学习数据</p>
      </div>
    );
  }

  const totalExercises = progress.reduce((sum, p) => sum + (p.totalCount || 0), 0);
  const completedExercises = progress.reduce((sum, p) => sum + (p.completedCount || 0), 0);
  const correctExercises = Math.round((completedExercises / Math.max(totalExercises, 1)) * (progress[0]?.correctRate || 0) / 100);
  const overallPct = totalExercises > 0 ? Math.round((completedExercises / totalExercises) * 100) : 0;

  return (
    <div>
      {/* Overall stats */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 mb-6">
        <StatCard label="总题数" value={totalExercises} color="#0056D2" />
        <StatCard label="已完成" value={completedExercises} color="#1890FF" />
        <StatCard label="正确数" value={correctExercises} color="#52C41A" />
        <StatCard label="完成率" value={`${overallPct}%`} color="#722ED1" />
      </div>

      {/* Per-chapter details */}
      <h3 className="text-sm font-semibold text-[#1a1a1a] mb-3">章节详情</h3>
      <div className="space-y-2">
        {progress.map(p => (
          <div key={p.chapterId} className="flex items-center gap-3 p-3 rounded-lg border border-[#E8E8E8] bg-white">
            <span className="text-sm text-[#1a1a1a] flex-1 truncate">{p.chapterName || '未命名章节'}</span>
            <span className="text-xs text-[#999]">{p.completedCount}/{p.totalCount} 题</span>
            <div className="w-24 h-2 bg-[#F0F0F0] rounded-full overflow-hidden">
              <div
                className="h-full bg-[#52C41A] rounded-full"
                style={{ width: `${p.totalCount ? Math.round(((p.completedCount || 0) / p.totalCount) * 100) : 0}%` }}
              />
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

function StatCard({ label, value, color }: { label: string; value: number | string; color: string }) {
  return (
    <div className="p-4 rounded-lg border border-[#E8E8E8] bg-white text-center">
      <div className="text-2xl font-bold" style={{ color }}>{value}</div>
      <div className="text-xs text-[#999] mt-1">{label}</div>
    </div>
  );
}
