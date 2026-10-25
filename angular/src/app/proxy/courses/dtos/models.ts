import type { TeachingClassMemberSource } from '../teaching-class-member-source.enum';
import type { EntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { ResourceType } from '../../resources/enums/resource-type.enum';
import type { CourseStatus } from '../enums/course-status.enum';
import type { TeachingClassStatus } from '../enums/teaching-class-status.enum';
import type { StudentCourseStatus } from '../../learning/enums/student-course-status.enum';

export interface AddTeachingClassMembersDto {
  studentIds?: string[];
  source?: TeachingClassMemberSource;
}

export interface AuditCourseDto {
  approved?: boolean;
  comment?: string | null;
}

export interface AuditResultDto {
  success?: boolean;
  message?: string | null;
}

export interface BatchEnrollDto {
  studentIds?: string[];
  courseId?: string;
  classId?: string | null;
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

export interface ChapterImportResultDto {
  totalRows?: number;
  successCount?: number;
  failCount?: number;
  errors?: string[];
}

export interface ChapterOrderDto {
  chapterId?: string;
  sortOrder?: number;
}

export interface ChapterResourceDto extends EntityDto<string> {
  chapterId?: string;
  resourceId?: string;
  displayName?: string | null;
  sortOrder?: number;
  resourceName?: string | null;
  originalFileName?: string | null;
  fileExtension?: string | null;
  fileSize?: number | null;
  resourceType?: ResourceType;
  isDownloadable?: boolean;
}

export interface CourseDetailDto extends CourseDto {
  chapters?: ChapterDto[];
}

export interface CourseDto extends FullAuditedEntityDto<string> {
  title?: string;
  description?: string | null;
  coverImageUrl?: string | null;
  majorId?: string | null;
  majorName?: string | null;
  libraryId?: string | null;
  majorIds?: string[];
  majorNames?: string[];
  semester?: string | null;
  credits?: number | null;
  semesterHours?: number | null;
  status?: CourseStatus;
  difficulty?: number;
  isRecommended?: boolean;
  isShared?: boolean;
  teacherId?: string | null;
  categoryId?: string | null;
  teacherName?: string | null;
  chapterCount?: number;
  studentCount?: number;
  isEnrolled?: boolean;
  progress?: number;
}

export interface CourseFilterDto {
  filter?: string | null;
  majorId?: string | null;
  majorIds?: string[] | null;
  semester?: string | null;
  difficulty?: number | null;
  categoryId?: string | null;
  teacherId?: string | null;
  status?: CourseStatus | null;
  isRecommended?: boolean | null;
  tenantId?: string | null;
}

export interface CourseResourceDto extends EntityDto<string> {
  courseId?: string;
  resourceId?: string;
  displayName?: string | null;
  sortOrder?: number;
  isRecommended?: boolean;
  creationTime?: string;
  resourceName?: string | null;
  description?: string | null;
  filePath?: string | null;
  keywords?: string | null;
  originalFileName?: string | null;
  fileExtension?: string | null;
  fileSize?: number | null;
  resourceType?: ResourceType;
  isDownloadable?: boolean;
}

export interface CourseShareDto extends EntityDto<string> {
  courseId?: string;
  courseTitle?: string | null;
  sourceTenantId?: string;
  sourceTenantName?: string | null;
  targetTenantId?: string;
  targetTenantName?: string | null;
  sharedByUserId?: string;
  sharedByUserName?: string | null;
  sharedAt?: string;
  note?: string | null;
}

export interface CreateChapterResourceDto {
  chapterId: string;
  resourceId: string;
  displayName?: string | null;
  sortOrder?: number;
}

export interface CreateCourseResourceDto {
  courseId: string;
  resourceId: string;
  displayName?: string | null;
  sortOrder?: number;
  isRecommended?: boolean;
}

export interface CreateCourseShareDto {
  courseId?: string;
  targetTenantIds?: string[];
  note?: string | null;
}

export interface CreateStudentCourseDto {
  studentId?: string;
  courseId?: string;
}

export interface CreateTeachingClassDto {
  name?: string;
  majorId?: string | null;
  courseId?: string | null;
  teacherId?: string | null;
  gradeYear?: number | null;
  description?: string | null;
  status?: TeachingClassStatus;
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
  majorId?: string | null;
  libraryId?: string | null;
  majorIds?: string[];
  semester?: string | null;
  credits?: number | null;
  semesterHours?: number | null;
  difficulty?: number;
  categoryId?: string | null;
  status?: CourseStatus;
  isRecommended?: boolean;
}

export interface CreateUpdateKnowledgeResourceDto {
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
  resourceId?: string | null;
}

export interface GetAvailableStudentsInput extends PagedAndSortedResultRequestDto {
  courseId?: string;
  tenantId?: string | null;
  filter?: string | null;
  majorId?: string | null;
  classId?: string | null;
}

export interface GetStudentCoursesInput extends PagedAndSortedResultRequestDto {
  courseId?: string | null;
  studentId?: string | null;
  status?: StudentCourseStatus | null;
  tenantId?: string | null;
  filter?: string | null;
}

export interface GetTeachingClassMembersInput extends PagedAndSortedResultRequestDto {
  classId?: string;
  filter?: string | null;
}

export interface GetTeachingClassesInput extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  majorId?: string | null;
  courseId?: string | null;
  teacherId?: string | null;
  status?: TeachingClassStatus | null;
  tenantId?: string | null;
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
  resourceId?: string | null;
  originalFileName?: string | null;
  fileExtension?: string | null;
  fileSize?: number | null;
  children?: KnowledgeResourceDto[];
}

export interface PagedCourseRequestDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  majorId?: string | null;
  majorIds?: string[] | null;
  onlyPublicCourses?: boolean | null;
  semester?: string | null;
  difficulty?: number | null;
  categoryId?: string | null;
  status?: CourseStatus | null;
  isRecommended?: boolean | null;
  tenantId?: string | null;
  teacherId?: string | null;
}

export interface PublishCourseInput {
  isShared?: boolean;
}

export interface RelatedChapterInfoDto {
  chapterId?: string;
  chapterTitle?: string;
}

export interface RelatedCourseInfoDto {
  courseId?: string;
  chapters?: RelatedChapterInfoDto[];
}

export interface RelatedCoursesResultDto {
  knowledgeResourceId?: string;
  name?: string;
  courses?: RelatedCourseInfoDto[];
}

export interface SharedCourseDto extends EntityDto<string> {
  shareId?: string;
  courseId?: string;
  title?: string;
  description?: string | null;
  coverImageUrl?: string | null;
  majorId?: string | null;
  majorName?: string | null;
  libraryId?: string | null;
  sourceTenantId?: string;
  sourceTenantName?: string | null;
  sharedAt?: string;
  sharedByUserId?: string;
  sharedByUserName?: string | null;
}

export interface SharedCourseListQueryDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  majorId?: string | null;
}

export interface StudentCourseDto extends FullAuditedEntityDto<string> {
  tenantId?: string | null;
  studentId?: string;
  studentName?: string | null;
  courseId?: string;
  courseName?: string | null;
  classId?: string | null;
  status?: StudentCourseStatus;
  enrolledAt?: string;
  progress?: number;
}

export interface TeachingClassDto extends EntityDto<string> {
  tenantId?: string | null;
  name?: string;
  majorId?: string | null;
  majorName?: string | null;
  courseId?: string | null;
  courseName?: string | null;
  teacherId?: string | null;
  teacherName?: string | null;
  gradeYear?: number | null;
  description?: string | null;
  status?: TeachingClassStatus;
  memberCount?: number;
  creationTime?: string;
}

export interface TeachingClassMemberDto extends EntityDto<string> {
  classId?: string;
  studentId?: string;
  studentName?: string | null;
  studentNumber?: string | null;
  userName?: string | null;
  majorName?: string | null;
  source?: TeachingClassMemberSource;
  joinedAt?: string;
}

export interface UpdateCourseResourceDto {
  displayName?: string | null;
  sortOrder?: number;
  isRecommended?: boolean;
}

export interface UpdateTeachingClassDto extends CreateTeachingClassDto {
}
