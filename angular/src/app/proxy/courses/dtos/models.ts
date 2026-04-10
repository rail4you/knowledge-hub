import type { EntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { CourseStatus } from '../enums/course-status.enum';
import type { StudentCourseStatus } from '../../learning/enums/student-course-status.enum';

export interface BatchEnrollDto {
  studentIds?: string[];
  courseId?: string;
}

export interface ChapterDto extends EntityDto<string> {
  courseId?: string;
  parentId?: string | null;
  title?: string;
  description?: string | null;
  sortOrder?: number;
  children?: ChapterDto[];
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

export interface CreateStudentCourseDto {
  studentId?: string;
  courseId?: string;
}

export interface CreateUpdateChapterDto {
  courseId?: string;
  parentId?: string | null;
  title?: string;
  description?: string | null;
  sortOrder?: number;
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
  status?: CourseStatus;
}

export interface GetAvailableStudentsInput extends PagedAndSortedResultRequestDto {
  courseId?: string;
  tenantId?: string | null;
  filter?: string | null;
}

export interface GetStudentCoursesInput extends PagedAndSortedResultRequestDto {
  courseId?: string | null;
  studentId?: string | null;
  status?: StudentCourseStatus | null;
  tenantId?: string | null;
  filter?: string | null;
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

export interface StudentCourseDto extends FullAuditedEntityDto<string> {
  tenantId?: string | null;
  studentId?: string;
  studentName?: string | null;
  courseId?: string;
  courseName?: string | null;
  status?: StudentCourseStatus;
  enrolledAt?: string;
  progress?: number;
}
