import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { NewsArticleStatus } from '../enums/news-article-status.enum';
import type { NewsCommentStatus } from '../enums/news-comment-status.enum';

export interface CreateNewsCommentDto {
  articleId?: string;
  parentId?: string | null;
  content?: string;
}

export interface CreateUpdateNewsArticleDto {
  categoryId?: string;
  title?: string;
  summary?: string | null;
  content?: string;
  coverImageUrl?: string | null;
  tags?: string | null;
  isTop?: boolean;
  isHot?: boolean;
  allowComments?: boolean;
}

export interface CreateUpdateNewsCategoryDto {
  parentId?: string | null;
  name?: string;
  code?: string;
  sortOrder?: number;
  isActive?: boolean;
}

export interface NewsArticleDto extends FullAuditedEntityDto<string> {
  categoryId?: string;
  categoryName?: string | null;
  title?: string;
  summary?: string | null;
  content?: string;
  coverImageUrl?: string | null;
  tags?: string | null;
  isTop?: boolean;
  isHot?: boolean;
  allowComments?: boolean;
  status?: NewsArticleStatus;
  authorId?: string | null;
  authorName?: string | null;
  publishedAt?: string | null;
  viewCount?: number;
  likeCount?: number;
  commentCount?: number;
  userHasLiked?: boolean;
}

export interface NewsCategoryDto extends FullAuditedEntityDto<string> {
  parentId?: string | null;
  name?: string;
  code?: string;
  sortOrder?: number;
  isActive?: boolean;
  children?: NewsCategoryDto[];
}

export interface NewsCommentDto extends FullAuditedEntityDto<string> {
  articleId?: string;
  parentId?: string | null;
  userId?: string;
  userName?: string | null;
  content?: string;
  status?: NewsCommentStatus;
}

export interface PagedNewsArticleRequestDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  categoryId?: string | null;
  status?: NewsArticleStatus | null;
}

export interface PagedNewsCommentRequestDto extends PagedAndSortedResultRequestDto {
  articleId?: string | null;
  status?: NewsCommentStatus | null;
  filter?: string | null;
}

export interface ReviewNewsArticleDto {
  comment?: string | null;
}

export interface ReviewNewsCommentDto {
  status?: NewsCommentStatus;
}
