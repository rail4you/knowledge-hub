import type { EntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { CourseStatus } from '../enums/course-status.enum';

export interface ChapterDto extends EntityDto<string> {
  courseId?: string;
  title?: string;
  description?: string | null;
  sortOrder?: number;
  knowledgeResources?: KnowledgeResourceDto[];
}

export interface CourseDetailDto extends CourseDto {
  chapters?: ChapterDto[];
}

export interface CourseDto extends FullAuditedEntityDto<string> {
  title?: string;
  description?: string | null;
  coverImageUrl?: string | null;
  major?: string | null;
  semester?: string | null;
  credits?: number | null;
  semesterHours?: number | null;
  status?: CourseStatus;
  difficulty?: number;
  teacherId?: string | null;
  categoryId?: string | null;
  teacherName?: string | null;
  chapterCount?: number;
  studentCount?: number;
}

export interface CourseFilterDto {
  filter?: string | null;
  major?: string | null;
  semester?: string | null;
  difficulty?: number | null;
  categoryId?: string | null;
  teacherId?: string | null;
  status?: CourseStatus | null;
}

export interface CreateUpdateCourseDto {
  title?: string;
  description?: string | null;
  coverImageUrl?: string | null;
  major?: string | null;
  semester?: string | null;
  credits?: number | null;
  semesterHours?: number | null;
  difficulty?: number;
  categoryId?: string | null;
}

export interface KnowledgeResourceDto extends FullAuditedEntityDto<string> {
  courseId?: string;
  chapterId?: string | null;
  name?: string;
  description?: string | null;
  content?: string | null;
  importanceLevel?: string;
  difficulty?: number;
  sortOrder?: number;
  tags?: string | null;
  parentId?: string | null;
  children?: KnowledgeResourceDto[];
}

export interface PagedCourseRequestDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  major?: string | null;
  semester?: string | null;
  difficulty?: number | null;
  categoryId?: string | null;
  status?: CourseStatus | null;
}
