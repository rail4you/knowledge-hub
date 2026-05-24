import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ArrowLeft, Eye, ThumbsUp, MessageSquare, Heart, Loader2, Send } from 'lucide-react';
import DOMPurify from 'dompurify';
import { useAuth } from '../lib/auth';
import { api, setTenantId } from '../lib/api';

interface NewsArticle {
  id: string;
  title: string;
  summary?: string | null;
  content?: string;
  coverImageUrl?: string | null;
  categoryName?: string | null;
  categoryId?: string;
  viewCount: number;
  likeCount: number;
  commentCount: number;
  userHasLiked?: boolean;
  isTop?: boolean;
  isHot?: boolean;
  authorName?: string;
  publishedAt?: string | null;
  creationTime?: string;
  allowComments?: boolean;
}

interface NewsComment {
  id: string;
  articleId: string;
  userId: string;
  userName?: string;
  content: string;
  creationTime: string;
}

export function NewsDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const auth = useAuth();

  const [article, setArticle] = useState<NewsArticle | null>(null);
  const [comments, setComments] = useState<NewsComment[]>([]);
  const [loading, setLoading] = useState(true);
  const [commentText, setCommentText] = useState('');
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (auth.currentTenantId) {
      setTenantId(auth.currentTenantId);
    }
    if (id) {
      loadArticle(id);
      loadComments(id);
    }
  }, [id, auth.currentTenantId]);

  const loadArticle = (articleId: string) => {
    setLoading(true);
    api.get(`/api/app/news-article/${articleId}`).then(res => {
      setArticle(res.data);
      setLoading(false);
    }).catch(() => {
      setLoading(false);
    });
  };

  const loadComments = (articleId: string) => {
    api.get(`/api/app/news-comment/approved-list-by-article/${articleId}`).then(res => {
      setComments(res.data || []);
    }).catch(() => {});
  };

  const goBack = () => {
    navigate('/student/news');
  };

  const likeArticle = () => {
    if (!article || article.userHasLiked) return;

    api.post(`/api/app/news-article/${article.id}/like`).then(() => {
      setArticle({
        ...article,
        userHasLiked: true,
        likeCount: article.likeCount + 1,
      });
    }).catch(() => {});
  };

  const submitComment = () => {
    if (!article || !commentText.trim()) return;

    setSubmitting(true);
    api.post('/api/app/news-comment', {
      articleId: article.id,
      content: commentText.trim(),
    }).then(res => {
      setComments([res.data, ...comments]);
      setArticle({
        ...article,
        commentCount: article.commentCount + 1,
      });
      setCommentText('');
      setSubmitting(false);
    }).catch(() => {
      setSubmitting(false);
    });
  };

  const formatDate = (dateStr?: string) => {
    if (!dateStr) return '';
    return new Date(dateStr).toLocaleString('zh-CN');
  };

  if (loading) {
    return (
      <div className="student-news-detail">
        <div className="state">
          <Loader2 className="h-8 w-8 animate-spin" />
        </div>
      </div>
    );
  }

  if (!article) {
    return (
      <div className="student-news-detail">
        <div className="state">
          <p>文章不存在</p>
        </div>
      </div>
    );
  }

  return (
    <div className="student-news-detail">
      <div className="back-btn">
        <button onClick={goBack} className="back-button">
          <ArrowLeft className="h-4 w-4" />
          返回资讯列表
        </button>
      </div>

      <div className="article-card">
        <div className="article-meta">
          {article.categoryName && <span className="category-tag">{article.categoryName}</span>}
          {article.isTop && <span className="category-tag top">置顶</span>}
          {article.isHot && <span className="category-tag hot">热门</span>}
        </div>

        <h1 className="article-title">{article.title}</h1>

        <div className="article-info">
          <span className="author">{article.authorName || '系统发布'}</span>
          <span className="divider">|</span>
          <span className="date">{formatDate(article.publishedAt || article.creationTime)}</span>
          <span className="divider">|</span>
          <span className="stats">
            <Eye className="h-4 w-4" /> {article.viewCount} 浏览
            <ThumbsUp className="h-4 w-4" /> {article.likeCount} 点赞
            <MessageSquare className="h-4 w-4" /> {article.commentCount} 评论
          </span>
        </div>

        {article.summary && (
          <div className="article-summary">
            <p>{article.summary}</p>
          </div>
        )}

        <div className="article-content">
          <div dangerouslySetInnerHTML={{ __html: DOMPurify.sanitize(article.content || '') }} />
        </div>

        <div className="article-actions">
          <button
            className={`like-btn ${article.userHasLiked ? 'liked' : ''}`}
            onClick={likeArticle}
            disabled={article.userHasLiked}
          >
            <Heart className="h-4 w-4" />
            {article.userHasLiked ? '已点赞' : '点赞'}
          </button>
        </div>
      </div>

      {article.allowComments && (
        <div className="comments-card">
          <h3 className="comments-title">评论区</h3>

          <div className="comment-form">
            <textarea
              value={commentText}
              onChange={e => setCommentText(e.target.value)}
              placeholder="输入评论内容..."
              rows={4}
            />
            <div className="comment-form__actions">
              <button
                className="submit-btn"
                onClick={submitComment}
                disabled={!commentText.trim() || submitting}
              >
                <Send className="h-4 w-4" />
                {submitting ? '提交中...' : '发表回复'}
              </button>
            </div>
          </div>

          <div className="comment-list">
            {comments.length === 0 ? (
              <div className="no-comments">暂无评论，快来抢沙发吧！</div>
            ) : (
              comments.map(comment => (
                <div key={comment.id} className="comment-item">
                  <div className="comment-item__avatar">
                    {(comment.userName || '匿名')[0]}
                  </div>
                  <div className="comment-item__body">
                    <div className="comment-item__header">
                      <span className="comment-item__name">{comment.userName || '匿名用户'}</span>
                      <span className="comment-item__time">{formatDate(comment.creationTime)}</span>
                    </div>
                    <div className="comment-item__content">{comment.content}</div>
                  </div>
                </div>
              ))
            )}
          </div>
        </div>
      )}
    </div>
  );
}