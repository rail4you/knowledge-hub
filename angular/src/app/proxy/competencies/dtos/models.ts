
export interface CompetencyDto {
  id?: string;
  majorId?: string | null;
  parentId?: string | null;
  level?: number;
  code?: string | null;
  name?: string;
  description?: string | null;
  sortOrder?: number;
  isEnabled?: boolean;
  courseCount?: number;
  knowledgeCount?: number;
  children?: CompetencyDto[];
}

export interface CompetencyKnowledgeDto {
  id?: string;
  competencyId?: string;
  knowledgeResourceId?: string;
  knowledgeResourceName?: string | null;
  chapterId?: string | null;
  weight?: number;
}

export interface CourseAchievementDto {
  courseId?: string;
  courseName?: string | null;
  studentCount?: number;
  measurableCount?: number;
  overallAchievement?: number;
  items?: CourseCompetencyDto[];
}

export interface CourseCompetencyDto {
  competencyId?: string;
  code?: string | null;
  name?: string;
  level?: number;
  weight?: number;
  knowledgeCount?: number;
  achievement?: number;
  studentCount?: number;
  note?: string | null;
}

export interface CourseCompetencyItemInput {
  competencyId?: string;
  weight?: number;
  note?: string | null;
}

export interface LinkCompetencyKnowledgeInput {
  competencyId?: string;
  knowledgeResourceId?: string;
  weight?: number;
}

export interface SaveCompetencyInput {
  majorId?: string | null;
  parentId?: string | null;
  level?: number;
  code?: string | null;
  name?: string;
  description?: string | null;
  sortOrder?: number;
  isEnabled?: boolean;
}

export interface SetCourseCompetencyInput {
  courseId?: string;
  items?: CourseCompetencyItemInput[];
}

export interface StudentAchievementDto {
  courseId?: string;
  studentId?: string;
  studentName?: string | null;
  overallAchievement?: number;
  items?: StudentCompetencyAchievementDto[];
}

export interface StudentCompetencyAchievementDto {
  competencyId?: string;
  code?: string | null;
  name?: string;
  weight?: number;
  achievement?: number;
  evidencedKnowledgeCount?: number;
  knowledgeCount?: number;
}
