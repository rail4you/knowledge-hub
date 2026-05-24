export interface PagedResult<T> {
  items: T[];
  totalCount: number;
}

export interface Course {
  id: string;
  title: string;
  description?: string | null;
  coverImageUrl?: string | null;
  major?: string | null;
  semester?: string | null;
  credits?: number | null;
  semesterHours?: number | null;
  difficulty: number;
  teacherName?: string | null;
  studentCount?: number;
  chapterCount?: number;
  creationTime?: string;
}

export interface Resource {
  id: string;
  name: string;
  description?: string | null;
  resourceType: number;
  categoryId?: string | null;
  categoryName?: string | null;
  fileExtension?: string | null;
  fileSize?: number | null;
  originalFileName?: string | null;
  collectionCount?: number;
  downloadCount?: number;
  viewCount?: number;
  creatorName?: string | null;
  creationTime?: string;
}

export interface NewsArticle {
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

export interface NewsComment {
  id: string;
  articleId: string;
  userId: string;
  userName?: string;
  content: string;
  creationTime: string;
}

export interface SearchResultItem {
  id: string;
  resourceId: string;
  resourceName: string;
  pageNumber: number;
  content: string;
  highlightedContent?: string | null;
  title?: string | null;
  relevanceScore: number;
  fileExtension: string;
  resourceType: number;
  categoryName?: string;
  uploadDate: string;
  sourceType?: 'document' | 'video';
  videoId?: string;
  videoName?: string;
  videoUrl?: string;
  startTime?: string;
  endTime?: string;
  eventDescription?: string;
}

export interface SearchHistory {
  id: string;
  queryText: string;
  creationTime: string;
  resultCount: number;
}

export interface ApplicationConfiguration {
  currentUser?: {
    isAuthenticated: boolean;
    userName?: string;
    name?: string;
    surName?: string;
    roles?: string[];
  };
}