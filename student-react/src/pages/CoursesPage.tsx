import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { BookOpen, PlayCircle, Clock, Users, Inbox, Search } from 'lucide-react';
import { useAuth } from '../lib/auth';
import { api, setTenantId } from '../lib/api';
import type { Course } from '../lib/types';

const difficultyLabels: Record<number, string> = { 1: '入门', 2: '初级', 3: '中级', 4: '高级', 5: '专家' };
const difficultyColors: Record<number, string> = {
  1: '#52c41a', 2: '#73d13d', 3: '#faad14', 4: '#fa8c16', 5: '#f5222d',
};

export function CoursesPage() {
  const auth = useAuth();
  const navigate = useNavigate();
  const [courses, setCourses] = useState<Course[]>([]);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState('');

  useEffect(() => {
    if (auth.currentTenantId) setTenantId(auth.currentTenantId);
  }, [auth.currentTenantId]);

  useEffect(() => { loadCourses(); }, []);

  const loadCourses = () => {
    setLoading(true);
    api.get('/api/app/course/my-courses', {
      params: { maxResultCount: 100, skipCount: 0, filter: filter || undefined },
    }).then(res => {
      setCourses(res.data?.items || []);
      setLoading(false);
    }).catch(() => setLoading(false));
  };

  const handleSearch = () => { loadCourses(); };

  if (!auth.isAuthenticated) {
    return (
      <div className="student-page">
        <div className="empty-state"><Inbox className="empty-state-icon" /><div className="empty-state-title">请先登录</div></div>
      </div>
    );
  }

  return (
    <div className="student-page">
      <div className="page-header">
        <h1 className="page-title">我的课程</h1>
        <p className="page-desc">查看已分配的课程，进行章节学习和习题练习</p>
      </div>

      {/* Search */}
      <div className="card p-md mb-lg">
        <div className="search-bar">
          <Search className="h-5 w-5 search-bar-icon" />
          <input type="text" value={filter} onChange={e => setFilter(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && handleSearch()}
            placeholder="搜索课程名称..." className="input" />
          <button onClick={handleSearch} className="btn btn-primary btn-sm">搜索</button>
        </div>
      </div>

      {loading ? (
        <div className="loading"><div className="loading-spinner" /></div>
      ) : courses.length === 0 ? (
        <div className="empty-state">
          <BookOpen className="empty-state-icon" />
          <div className="empty-state-title">暂无课程</div>
          <div className="empty-state-desc">尚未分配课程，请联系教师</div>
        </div>
      ) : (
        <div className="course-grid">
          {courses.map(course => (
            <div key={course.id} className="course-card" onClick={() => navigate(`/student/course-detail/${course.id}`)}>
              {/* Cover */}
              <div className="course-card-cover" style={{
                background: course.coverImageUrl
                  ? `url(${course.coverImageUrl}) center/cover`
                  : 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
              }}>
                {!course.coverImageUrl && <BookOpen className="h-10 w-10 text-white/70" />}
                <span className="course-card-badge" style={{ background: difficultyColors[course.difficulty] || '#999' }}>
                  {difficultyLabels[course.difficulty] || '未知'}
                </span>
              </div>

              {/* Body */}
              <div className="course-card-body">
                <h3 className="course-card-title">{course.title}</h3>
                <p className="course-card-desc">{course.description || '暂无课程简介'}</p>

                <div className="course-card-meta">
                  {course.teacherName && (
                    <span><Users className="h-3.5 w-3.5" /> {course.teacherName}</span>
                  )}
                  {course.studentCount != null && (
                    <span><Users className="h-3.5 w-3.5" /> {course.studentCount} 人</span>
                  )}
                  {course.semester && (
                    <span><Clock className="h-3.5 w-3.5" /> {course.semester}</span>
                  )}
                </div>

                {/* Progress */}
                {course.isEnrolled && course.progress != null && (
                  <div className="course-progress">
                    <div className="course-progress-bar">
                      <div className="course-progress-fill" style={{ width: `${course.progress}%` }} />
                    </div>
                    <span className="course-progress-text">{course.progress}%</span>
                  </div>
                )}

                <div className="course-card-footer">
                  <button className="btn btn-primary btn-sm" onClick={e => { e.stopPropagation(); navigate(`/student/course-detail/${course.id}`); }}>
                    <PlayCircle className="h-4 w-4" /> 进入学习
                  </button>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
