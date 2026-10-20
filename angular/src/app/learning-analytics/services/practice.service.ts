import { Injectable, inject } from '@angular/core';
import { RestService } from '@abp/ng.core';
import { Observable } from 'rxjs';

/** 专项练习题目 */
export interface PracticeExercise {
  id: string;
  title: string;
  questionContent: string;
  /** 0 单选 / 1 多选 / 2 判断 / 3 填空 / 4 简答 / 5 论述 / 8 计算 ... */
  type: number;
  options?: string | null;
  score: number;
  difficulty: number;
  chapterTitle?: string | null;
  knowledgePointNames: string[];
}

export interface PracticeSet {
  courseId: string;
  courseName: string;
  knowledgePointId?: string | null;
  knowledgePointName?: string | null;
  reason?: string | null;
  items: PracticeExercise[];
  message?: string | null;
}

export interface PracticeMasteryItem {
  knowledgePointId: string;
  name: string;
  mastery: number;
  evidence: number;
  level: number;
  targetLine: number;
}

export interface PracticeItemResult {
  exerciseId: string;
  title: string;
  studentAnswer?: string | null;
  correctAnswer?: string | null;
  isCorrect?: boolean | null;
  analysis?: string | null;
}

export interface PracticeResult {
  totalCount: number;
  correctCount: number;
  wrongCount: number;
  knowledgePoints: PracticeMasteryItem[];
  items: PracticeItemResult[];
}

/** 学生端专项练习（推荐 → 作答 → 掌握度回写） */
@Injectable({ providedIn: 'root' })
export class PracticeService {
  private readonly restService = inject(RestService);
  private readonly apiName = 'KnowledgeHub';
  private readonly base = '/api/app/practice';

  private api<T>(request: any): Observable<T> {
    return this.restService.request(request, { apiName: this.apiName }) as Observable<T>;
  }

  getSet(courseId: string, knowledgePointId?: string, take = 6): Observable<PracticeSet> {
    return this.api<PracticeSet>({
      method: 'GET',
      url: `${this.base}/set/${courseId}`,
      params: {
        knowledgePoint: knowledgePointId || undefined,
        take,
      },
    });
  }

  submit(input: {
    courseId: string;
    knowledgePointId?: string | null;
    answers: { exerciseId: string; answer: string | null; timeSpentTicks: number }[];
  }): Observable<PracticeResult> {
    return this.api<PracticeResult>({ method: 'POST', url: `${this.base}/submit`, body: input });
  }
}
