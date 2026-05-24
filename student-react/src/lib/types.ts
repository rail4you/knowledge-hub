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
  isEnrolled?: boolean;
  progress?: number;
  status?: number;
  creationTime?: string;
}

export interface CourseDetail {
  id?: string;
  title?: string;
  description?: string;
  coverImageUrl?: string;
  major?: string;
  semester?: string;
  credits?: number;
  semesterHours?: number;
  difficulty?: number;
  teacherName?: string;
  studentCount?: number;
  isEnrolled?: boolean;
  progress?: number;
  chapters?: ChapterDto[];
}

export interface ChapterDto {
  id: string;
  courseId?: string;
  parentId?: string;
  title?: string;
  description?: string;
  sortOrder?: number;
  children?: ChapterDto[];
}

export interface ExerciseDto {
  id: string;
  courseId?: string;
  chapterId?: string;
  title?: string;
  questionContent?: string;
  type: ExerciseType;
  options?: string;
  answer?: string;
  answerExplanation?: string;
  difficulty: number;
  score: number;
  sortOrder?: number;
}

export enum ExerciseType {
  SingleChoice = 0,
  MultiChoice = 1,
  TrueFalse = 2,
  FillBlank = 3,
  ShortAnswer = 4,
  Essay = 5,
  CaseAnalysis = 6,
}

export interface StudentExerciseRecordDto {
  id?: string;
  exerciseId?: string;
  courseId?: string;
  chapterId?: string;
  studentAnswer?: string;
  isCorrect?: boolean;
  score?: number;
  timeSpentTicks?: number;
  selfAssessment?: SelfAssessment;
  completedAt?: string;
  hasViewedAnswer?: boolean;
  creationTime?: string;
}

export enum SelfAssessment {
  None = 0,
  Correct = 1,
  PartiallyCorrect = 2,
  Incorrect = 3,
}

export interface SaveExerciseRecordInput {
  courseId: string;
  chapterId?: string;
  exerciseId: string;
  studentAnswer: string;
  timeSpentTicks: number;
}

export interface ChapterProgressDto {
  chapterId?: string;
  chapterTitle?: string;
  totalExercises?: number;
  completedExercises?: number;
  correctExercises?: number;
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
