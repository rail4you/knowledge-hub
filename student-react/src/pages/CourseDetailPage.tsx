import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import {
  ArrowLeft, BookOpen, Users, Clock, PlayCircle, Network, ChevronRight, ChevronDown,
  FileText, Inbox, Loader2,
} from 'lucide-react';
import { useAuth } from '../lib/auth';
import { api, setTenantId } from '../lib/api';
import type { CourseDetail, ChapterDto, ChapterProgressDto } from '../lib/types';

const difficultyLabels: Record<number, string> = { 1: '入门', 2: '初级', 3: '中级', 4: '高级', 5: '专家' };
const difficultyColors: Record<number, string> = {
  1: '#52c41a', 2: '#73d13d', 3: '#faad14', 4: '#fa8c16', 5: '#f5222d',
};

export function CourseDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const auth = useAuth();

  const [course, setCourse] = useState<CourseDetail | null>(null);
  const [chapters, setChapters] = useState<ChapterDto[]>([]);
  const [progress, setProgress] = useState<ChapterProgressDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [expanded, setExpanded] = useState<Set<string>>(new Set());

  useEffect(() => {
    if (auth.currentTenantId) setTenantId(auth.currentTenantId);
    if (id) loadData(id);
  }, [id, auth.currentTenantId]);

  const loadData = (courseId: string) => {
    setLoading(true);
    Promise.all([
      api.get(`/api/app/course/${courseId}/detail`),
      api.get(`/api/app/chapter/chapter-tree/${courseId}`),
      api.get(`/api/app/student-exercise-record/chapter-progress/${courseId}`).catch(() => ({ data: { items: [] } })),
    ]).then(([courseRes, chapterRes, progressRes]) => {
      setCourse(courseRes.data);
      setChapters(chapterRes.data || []);
      setProgress(progressRes.data?.items || []);
      setLoading(false);
    }).catch(() => setLoading(false));
  };

  const toggleExpand = (nodeId: string) => {
    setExpanded(prev => {
      const next = new Set(prev);
      next.has(nodeId) ? next.delete(nodeId) : next.add(nodeId);
      return next;
    });
  };

  const getChapterProgress = (chapterId: string) => {
    const p = progress.find(x => x.chapterId === chapterId);
    if (!p || !p.totalExercises) return null;
    return {
      total: p.totalExercises,
      completed: p.completedExercises ?? 0,
      correct: p.correctExercises ?? 0,
      percent: Math.round(((p.completedExercises ?? 0) / p.totalExercises) * 100),
    };
  };

  if (loading) {
    return <div className="student-page"><div className="loading"><div className="loading-spinner" /></div></div>;
  }
  if (!course) {
    return <div className="student-page"><div className="empty-state"><Inbox className="empty-state-icon" /><div className="empty-state-title">课程不存在</div></div></div>;
  }

  return (
    <div className="student-page">
      {/* Back */}
      <button onClick={() => navigate('/student/courses')} className="btn btn-default btn-sm mb-lg">
        <ArrowLeft className="h-4 w-4" /> 返回课程列表
      </button>

      {/* Course Header */}
      <div className="course-detail-header">
        <div className="course-detail-cover" style={{
          background: course.coverImageUrl
            ? `url(${course.coverImageUrl}) center/cover`
            : 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
        }}>
          {!course.coverImageUrl && <BookOpen className="h-16 w-16 text-white/60" />}
        </div>
        <div className="course-detail-info">
          <h1>{course.title}</h1>
          {course.description && <p className="course-detail-desc">{course.description}</p>}
          <div className="course-detail-tags">
            {course.difficulty && (
              <span className="tag" style={{ background: difficultyColors[course.difficulty], color: '#fff' }}>
                {difficultyLabels[course.difficulty]}
              </span>
            )}
            {course.major && <span className="tag tag-primary">{course.major}</span>}
            {course.credits && <span className="tag tag-purple">{course.credits} 学分</span>}
          </div>
          <div className="course-detail-meta">
            {course.teacherName && <span><Users className="h-4 w-4" /> {course.teacherName}</span>}
            {course.studentCount != null && <span><Users className="h-4 w-4" /> {course.studentCount} 人学习</span>}
            {course.semester && <span><Clock className="h-4 w-4" /> {course.semester}</span>}
          </div>
          {course.isEnrolled && course.progress != null && (
            <div className="course-progress mt-md">
              <span className="text-secondary mr-sm">学习进度</span>
              <div className="course-progress-bar" style={{ flex: 1 }}>
                <div className="course-progress-fill" style={{ width: `${course.progress}%` }} />
              </div>
              <span className="course-progress-text">{course.progress}%</span>
            </div>
          )}

          {/* Action buttons */}
          <div className="course-detail-actions">
            <button className="btn btn-primary" onClick={() => navigate(`/student/exercise-learning/${id}`)}>
              <PlayCircle className="h-4 w-4" /> 开始练习
            </button>
            <button className="btn btn-default" onClick={() => navigate(`/student/knowledge-graph/${id}`)}>
              <Network className="h-4 w-4" /> 知识图谱
            </button>
          </div>
        </div>
      </div>

      {/* Chapter tree */}
      <div className="card mt-lg">
        <div className="card-header">
          <h2 className="card-title">课程章节</h2>
        </div>
        <div className="card-body">
          {chapters.length === 0 ? (
            <div className="empty-state" style={{ padding: '40px 20px' }}>
              <FileText className="empty-state-icon" />
              <div className="empty-state-title">暂无章节</div>
            </div>
          ) : (
            <div className="chapter-tree">
              {chapters.map(ch => (
                <ChapterNode
                  key={ch.id}
                  chapter={ch}
                  depth={0}
                  expanded={expanded}
                  onToggle={toggleExpand}
                  getProgress={getChapterProgress}
                  courseId={id!}
                />
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

/* --- Chapter tree node --- */
function ChapterNode({ chapter, depth, expanded, onToggle, getProgress, courseId }: {
  chapter: ChapterDto;
  depth: number;
  expanded: Set<string>;
  onToggle: (id: string) => void;
  getProgress: (id: string) => { total: number; completed: number; correct: number; percent: number } | null;
  courseId: string;
}) {
  const navigate = useNavigate();
  const hasChildren = chapter.children && chapter.children.length > 0;
  const isExpanded = expanded.has(chapter.id);
  const progress = getProgress(chapter.id);

  return (
    <div className="chapter-node">
      <div
        className="chapter-node-content"
        style={{ paddingLeft: depth * 24 }}
      >
        {hasChildren ? (
          <button className="chapter-toggle" onClick={() => onToggle(chapter.id)}>
            {isExpanded ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
          </button>
        ) : (
          <span className="chapter-toggle placeholder"><FileText className="h-4 w-4" /></span>
        )}
        <span className="chapter-title">{chapter.title}</span>
        {progress && (
          <span className="chapter-progress-badge">
            {progress.completed}/{progress.total} 题
            <span className="chapter-progress-mini">
              <span className="chapter-progress-mini-fill" style={{ width: `${progress.percent}%` }} />
            </span>
          </span>
        )}
        {progress && progress.total > 0 && (
          <button
            className="btn btn-sm btn-default ml-auto"
            onClick={() => navigate(`/student/exercise-learning/${courseId}?chapterId=${chapter.id}`)}
          >
            <PlayCircle className="h-3.5 w-3.5" /> 练习
          </button>
        )}
      </div>
      {hasChildren && isExpanded && chapter.children!.map(child => (
        <ChapterNode
          key={child.id}
          chapter={child}
          depth={depth + 1}
          expanded={expanded}
          onToggle={onToggle}
          getProgress={getProgress}
          courseId={courseId}
        />
      ))}
    </div>
  );
}
