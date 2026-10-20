import { Injectable, inject } from '@angular/core';
import { RestService } from '@abp/ng.core';
import { Observable, catchError, of } from 'rxjs';

/** 知识点管理（归一化闭环：确认灰区 / 合并 / 拆分 / 习题关联） */
export interface KnowledgePointDetail {
  id: string;
  courseId: string;
  chapterId?: string | null;
  chapterTitle?: string | null;
  name: string;
  description?: string | null;
  syllabusOrder: number;
  /** 0 已确认 / 1 待确认 */
  status: number;
  /** Seed / Llm / Manual */
  source: string;
  canonicalId?: string | null;
  canonicalName?: string | null;
  exerciseCount: number;
  prerequisiteOutCount: number;
  prerequisiteInCount: number;
  hasEmbedding: boolean;
  /** 是否有知识点级判定线覆盖 */
  hasCustomThreshold: boolean;
  /** 生效达标线（0~1） */
  targetLine: number;
  /** 生效薄弱线（0~1） */
  weakLine: number;
  creationTime: string;
}

export interface KpExerciseLink {
  id: string;
  exerciseId: string;
  exerciseTitle: string;
  questionContent?: string | null;
  chapterId?: string | null;
  chapterTitle?: string | null;
  exerciseScore: number;
  weight: number;
  source: string;
}

export interface KpMergeSuggestion {
  sourceId: string;
  sourceName: string;
  sourceStatus: number;
  targetId: string;
  targetName: string;
  targetStatus: number;
  similarity: number;
  /** embedding / char */
  method: string;
}

/** 向量引擎状态 */
export interface KnowledgeVectorStatus {
  /** pgvector / memory */
  engine: string;
  dimension?: number | null;
  available: boolean;
  embeddedCount: number;
  knowledgePointCount: number;
  message?: string | null;
}

export interface RebuildEmbeddingsResult {
  total: number;
  updated: number;
  provider: string;
  /** pgvector / memory */
  engine: string;
  dimension?: number | null;
  vectorStored: number;
  message: string;
}

/** 图谱节点（Kp 为完整详情，便于直接复用管理页的操作面板） */
export interface KnowledgePointGraphNode {
  kp: KnowledgePointDetail;
  isExternal: boolean;
  courseTitle?: string | null;
  level: number;
}

export interface KnowledgePointGraphEdge {
  fromId: string;
  toId: string;
  /** prerequisite 先修 / merge 已合并 */
  type: 'prerequisite' | 'merge';
  status: number;
  strength: number;
  note?: string | null;
}

export interface KnowledgePointGraph {
  courseId: string;
  courseName: string;
  nodes: KnowledgePointGraphNode[];
  edges: KnowledgePointGraphEdge[];
  externalNodeCount: number;
  proposedEdgeCount: number;
  mergedNodeCount: number;
}

export interface CourseExerciseItem {
  id: string;
  title: string;
  questionContent?: string;
  chapterId?: string | null;
  score?: number;
}

@Injectable({ providedIn: 'root' })
export class KnowledgePointService {
  private readonly restService = inject(RestService);
  private readonly apiName = 'KnowledgeHub';
  private readonly base = '/api/app/knowledge-point';

  private api<T>(request: any): Observable<T> {
    return this.restService.request(request, { apiName: this.apiName }) as Observable<T>;
  }

  getList(params: {
    courseId: string;
    filter?: string;
    status?: number | null;
    source?: string | null;
    skipCount: number;
    maxResultCount: number;
  }): Observable<{ totalCount: number; items: KnowledgePointDetail[] }> {
    return this.api({
      method: 'GET',
      url: this.base,
      params: {
        courseId: params.courseId,
        filter: params.filter || undefined,
        status: params.status ?? undefined,
        source: params.source || undefined,
        skipCount: params.skipCount,
        maxResultCount: params.maxResultCount,
      },
    });
  }

  update(
    id: string,
    input: { name: string; chapterId?: string | null; description?: string | null; syllabusOrder: number }
  ): Observable<KnowledgePointDetail> {
    return this.api({ method: 'PUT', url: `${this.base}/${id}`, body: input });
  }

  confirm(id: string): Observable<KnowledgePointDetail> {
    return this.api({ method: 'POST', url: `${this.base}/${id}/confirm` });
  }

  merge(sourceId: string, targetId: string): Observable<{ message: string }> {
    return this.api({ method: 'POST', url: `${this.base}/merge`, body: { sourceId, targetId } });
  }

  split(input: {
    knowledgePointId: string;
    newName: string;
    chapterId?: string | null;
    exerciseIds: string[];
  }): Observable<KnowledgePointDetail> {
    return this.api({ method: 'POST', url: `${this.base}/split`, body: input });
  }

  remove(id: string): Observable<void> {
    return this.api({ method: 'DELETE', url: `${this.base}/${id}` });
  }

  // ── 习题关联 ──
  getExerciseLinks(knowledgePointId: string): Observable<KpExerciseLink[]> {
    return this.api({ method: 'GET', url: `${this.base}/exercise-links/${knowledgePointId}` });
  }

  addExerciseLink(input: { knowledgePointId: string; exerciseId: string; weight: number }): Observable<KpExerciseLink> {
    return this.api({ method: 'POST', url: `${this.base}/exercise-link`, body: input });
  }

  updateExerciseLink(input: { linkId: string; weight: number }): Observable<KpExerciseLink> {
    return this.api({ method: 'PUT', url: `${this.base}/exercise-link`, body: input });
  }

  removeExerciseLink(linkId: string): Observable<void> {
    return this.api({ method: 'DELETE', url: `${this.base}/exercise-link/${linkId}` });
  }

  /** 知识点图谱（先修关系 + 合并关系） */
  getGraph(courseId: string): Observable<KnowledgePointGraph> {
    return this.api<KnowledgePointGraph>({ method: 'GET', url: `${this.base}/graph/${courseId}` });
  }

  // ── 归一化辅助 ──
  /** 合并建议；scope=course（同课程）| tenant（跨课程，使用 pgvector ANN） */
  getMergeSuggestions(
    courseId: string,
    threshold = 0.75,
    scope: 'course' | 'tenant' = 'course'
  ): Observable<KpMergeSuggestion[]> {
    return this.api<KpMergeSuggestion[]>({
      method: 'GET',
      url: `${this.base}/merge-suggestions/${courseId}`,
      params: { threshold, scope },
    }).pipe(catchError(() => of([] as KpMergeSuggestion[])));
  }

  /** 向量引擎状态（pgvector / memory、维度、已入库向量数） */
  getVectorStatus(courseId?: string): Observable<KnowledgeVectorStatus> {
    return this.api<KnowledgeVectorStatus>({
      method: 'GET',
      url: `${this.base}/vector-status`,
      params: courseId ? { courseId } : undefined,
    }).pipe(
      catchError(() =>
        of({
          engine: 'memory',
          dimension: null,
          available: false,
          embeddedCount: 0,
          knowledgePointCount: 0,
          message: '无法获取向量引擎状态',
        } as KnowledgeVectorStatus)
      )
    );
  }

  rebuildEmbeddings(courseId: string): Observable<RebuildEmbeddingsResult> {
    return this.api<RebuildEmbeddingsResult>({ method: 'POST', url: `${this.base}/rebuild-embeddings/${courseId}` });
  }
}
