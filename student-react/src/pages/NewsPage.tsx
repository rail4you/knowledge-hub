import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { FileText, Eye, MessageSquare, ThumbsUp, Calendar, Inbox, Search, Flame } from 'lucide-react';
import { useAuth } from '../lib/auth';
import { api, setTenantId } from '../lib/api';

interface Article {
  id: string;
  title: string;
  summary?: string | null;
  categoryName?: string | null;
  viewCount?: number;
  likeCount?: number;
  commentCount?: number;
  publishedAt?: string | null;
  creationTime?: string;
  isTop?: boolean;
  isHot?: boolean;
  authorName?: string;
}

interface Category {
  id: string;
  name: string;
  children?: Category[];
}

export function NewsPage() {
  const auth = useAuth();
  const [articles, setArticles] = useState<Article[]>([]);
  const [hotArticles, setHotArticles] = useState<Article[]>([]);
  const [categories, setCategories] = useState<Category[]>([]);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState('');
  const [selectedCategory, setSelectedCategory] = useState<string | null>(null);

  useEffect(() => {
    if (auth.currentTenantId) {
      setTenantId(auth.currentTenantId);
    }
    loadCategories();
    loadArticles();
    loadHotArticles();
  }, []);

  const loadCategories = () => {
    api.get('/api/app/news/categories/tree').then(res => {
      setCategories(res.data || []);
    }).catch(() => {});
  };

  const loadArticles = () => {
    setLoading(true);
    const params: any = { skipCount: 0, maxResultCount: 30 };
    if (filter) params.filter = filter;
    if (selectedCategory) params.categoryId = selectedCategory;

    api.get('/api/app/news/published', { params }).then(res => {
      setArticles(res.data?.items || []);
      setLoading(false);
    }).catch(() => setLoading(false));
  };

  const loadHotArticles = () => {
    api.get('/api/app/news/hot').then(res => {
      setHotArticles(res.data || []);
    }).catch(() => {});
  };

  const flattenCategories = (cats: Category[]): Category[] => {
    const result: Category[] = [];
    cats.forEach(c => {
      result.push(c);
      if (c.children?.length) {
        result.push(...flattenCategories(c.children));
      }
    });
    return result;
  };

  if (!auth.isAuthenticated) {
    return (
      <div className="student-page">
        <div className="empty-state">
          <Inbox className="empty-state-icon" />
          <div className="empty-state-title">请先登录</div>
          <div className="empty-state-desc">登录后可访问资讯中心</div>
        </div>
      </div>
    );
  }

  return (
    <div className="student-page">
      {/* 页面头部 */}
      <div className="page-header">
        <h1 className="page-title">资讯中心</h1>
        <p className="page-desc">查看行业动态、政策解读、教学资讯与企业资讯</p>
      </div>

      <div className="content-layout">
        {/* 主内容 */}
        <div className="content-main">
          {/* 搜索和筛选 */}
          <div className="filters-row">
            <div className="search-bar" style={{ flex: 1, maxWidth: '400px' }}>
              <Search className="h-5 w-5 search-bar-icon" />
              <input
                type="text"
                value={filter}
                onChange={e => setFilter(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && loadArticles()}
                placeholder="搜索标题、摘要或标签"
                className="input"
              />
              <button onClick={loadArticles} className="btn btn-primary btn-sm">筛选</button>
            </div>
            <select
              value={selectedCategory || ''}
              onChange={e => { setSelectedCategory(e.target.value || null); }}
              className="select"
              style={{ width: '160px' }}
            >
              <option value="">全部分类</option>
              {flattenCategories(categories).map(cat => (
                <option key={cat.id} value={cat.id}>{cat.name}</option>
              ))}
            </select>
          </div>

          {/* 文章列表 */}
          {loading ? (
            <div className="loading">
              <div className="loading-spinner" />
            </div>
          ) : articles.length === 0 ? (
            <div className="empty-state">
              <Inbox className="empty-state-icon" />
              <div className="empty-state-title">暂无资讯</div>
            </div>
          ) : (
            <div className="article-list">
              {articles.map(article => (
                <Link key={article.id} to={`/student/news/${article.id}`} className="article-card">
                  <div className="article-header">
                    <div className="article-meta">
                      {article.categoryName && <span className="tag tag-primary">{article.categoryName}</span>}
                      {article.isTop && <span className="tag tag-danger">置顶</span>}
                      {article.isHot && <span className="tag tag-warning">热门</span>}
                    </div>
                    <span className="article-date">
                      {article.publishedAt || article.creationTime?.split('T')[0]}
                    </span>
                  </div>
                  <h3 className="article-title">{article.title}</h3>
                  {article.summary && <p className="article-summary">{article.summary}</p>}
                  <div className="article-footer">
                    <span className="text-muted">{article.authorName || '系统发布'}</span>
                    <div className="article-stats">
                      <span><Eye className="h-4 w-4" /> {article.viewCount}</span>
                      <span><ThumbsUp className="h-4 w-4" /> {article.likeCount}</span>
                      <span><MessageSquare className="h-4 w-4" /> {article.commentCount}</span>
                    </div>
                  </div>
                </Link>
              ))}
            </div>
          )}
        </div>

        {/* 侧边栏 */}
        <aside className="sidebar-card" style={{ width: '300px', flexShrink: 0 }}>
          <div className="sidebar-card">
            <div className="sidebar-card-title">
              <Flame className="h-4 w-4 inline mr-2 text-orange-500" />
              热门资讯
            </div>
            {hotArticles.length === 0 ? (
              <div className="text-muted text-center py-lg">暂无热门资讯</div>
            ) : (
              <div className="hot-list">
                {hotArticles.map(article => (
                  <Link key={article.id} to={`/student/news/${article.id}`} className="hot-item">
                    <div className="hot-item-title">{article.title}</div>
                    <div className="hot-item-views">{article.viewCount} 浏览</div>
                  </Link>
                ))}
              </div>
            )}
          </div>
        </aside>
      </div>
    </div>
  );
}