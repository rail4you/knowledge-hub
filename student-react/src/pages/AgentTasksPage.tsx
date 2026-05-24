import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { Bot, Inbox, Loader2 } from 'lucide-react';
import { useAuth } from '../lib/auth';
import { api, setTenantId } from '../lib/api';

interface AgentTask {
  assignmentId: string;
  title: string;
  description?: string;
  teachingAgentName: string;
  status: number;
  dueTime?: string;
  lastActiveAt?: string;
}

const statusMap: Record<number, { text: string; className: string }> = {
  1: { text: '进行中', className: 'pending' },
  2: { text: '已完成', className: 'completed' },
  3: { text: '已超时', className: 'overdue' },
};

export function AgentTasksPage() {
  const auth = useAuth();
  const [tasks, setTasks] = useState<AgentTask[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (auth.currentTenantId) {
      setTenantId(auth.currentTenantId);
    }
    loadTasks();
  }, [auth.currentTenantId]);

  const loadTasks = () => {
    setLoading(true);
    api.get('/api/app/teaching-agent/student-assignments').then(res => {
      setTasks(res.data || []);
      setLoading(false);
    }).catch(() => setLoading(false));
  };

  const formatDate = (dateStr?: string) => {
    if (!dateStr) return '-';
    return new Date(dateStr).toLocaleDateString('zh-CN');
  };

  if (!auth.isAuthenticated) {
    return (
      <div className="student-page">
        <div className="empty-state">
          <Inbox className="empty-state-icon" />
          <div className="empty-state-title">请先登录</div>
          <div className="empty-state-desc">登录后可访问课堂任务</div>
        </div>
      </div>
    );
  }

  return (
    <div className="student-page">
      {/* 页面头部 */}
      <div className="page-header">
        <div>
          <h1 className="page-title">智能体交互任务</h1>
          <p className="page-desc">进入任务后可直接与课堂智能体对话，并提交完成摘要或请求教师帮助</p>
        </div>
      </div>

      {loading ? (
        <div className="loading">
          <div className="loading-spinner" />
        </div>
      ) : tasks.length === 0 ? (
        <div className="empty-state">
          <Bot className="empty-state-icon" />
          <div className="empty-state-title">当前没有待处理任务</div>
          <div className="empty-state-desc">等待教师发布任务后即可开始</div>
        </div>
      ) : (
        <div className="task-table">
          <table>
            <thead>
              <tr>
                <th>任务名称</th>
                <th>智能体</th>
                <th>状态</th>
                <th>截止时间</th>
                <th>最近活跃</th>
                <th>操作</th>
              </tr>
            </thead>
            <tbody>
              {tasks.map(task => (
                <tr key={task.assignmentId}>
                  <td>
                    <div className="font-semibold">{task.title}</div>
                    {task.description && (
                      <div className="text-muted text-sm mt-sm">{task.description}</div>
                    )}
                  </td>
                  <td>{task.teachingAgentName}</td>
                  <td>
                    <span className={`task-status ${statusMap[task.status]?.className || ''}`}>
                      {statusMap[task.status]?.text || '未知'}
                    </span>
                  </td>
                  <td>{formatDate(task.dueTime)}</td>
                  <td>{formatDate(task.lastActiveAt)}</td>
                  <td>
                    <Link
                      to={`/student/agent-tasks/${task.assignmentId}`}
                      className="btn btn-primary btn-sm"
                    >
                      进入任务
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}