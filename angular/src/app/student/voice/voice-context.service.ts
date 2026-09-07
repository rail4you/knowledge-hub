import { Injectable } from '@angular/core';

/**
 * 语音助手 MVP：页面上下文快照。
 * 首批只覆盖 3 个只读页：课程中心 / 课程详情 / 章节学习。
 * 各页面组件主动 register，助手按“最后注册优先”取快照，取不到则走 DOM 兜底。
 */
export interface VoiceContextItem {
  id: string;
  title: string;
  extra?: string;
}

export interface VoicePageSnapshot {
  /** 注册键，如 'courses' / 'course-detail' / 'course-learn' */
  key: string;
  /** 当前路由，如 /student/courses */
  route: string;
  /** 口语标题，如“课程中心”“《Python基础》课程详情” */
  title: string;
  /** 200-500 字的结构化摘要正文（给 AI 当上下文，也可直接朗读） */
  summary: string;
  /** 可跳转条目（课程列表等），用于“打开第X门” */
  items: VoiceContextItem[];
  /** 当前课程 id（详情/学习页有） */
  courseId?: string | null;
  /** 当前章节 id（学习页有） */
  chapterId?: string | null;
}

export type VoiceContextProvider = () => VoicePageSnapshot | null;

const DOM_MAX_CHARS = 2000;

@Injectable({ providedIn: 'root' })
export class VoiceContextService {
  private readonly providers = new Map<string, VoiceContextProvider>();

  register(key: string, provider: VoiceContextProvider): void {
    this.providers.set(key, provider);
  }

  unregister(key: string): void {
    this.providers.delete(key);
  }

  /** 按注册逆序取第一个非空快照；都没有则用 DOM 兜底。 */
  snapshot(currentRoute: string): VoicePageSnapshot {
    const keys = Array.from(this.providers.keys()).reverse();
    for (const key of keys) {
      try {
        const snap = this.providers.get(key)?.();
        if (snap) return snap;
      } catch {
        // 忽略单个 provider 异常，继续尝试下一个
      }
    }
    return this.snapshotFromDom(currentRoute);
  }

  private snapshotFromDom(currentRoute: string): VoicePageSnapshot {
    const doc = typeof document !== 'undefined' ? document : null;
    const h1 = doc?.querySelector('main h1, h1')?.textContent?.trim() || '';
    const mainText = (doc?.querySelector('main')?.textContent || doc?.body?.textContent || '')
      .replace(/\s+/g, ' ')
      .trim()
      .slice(0, DOM_MAX_CHARS);
    const title = h1 || this.routeLabel(currentRoute);
    return {
      key: 'dom-fallback',
      route: currentRoute,
      title,
      summary: mainText ? `页面标题：${title}。页面正文摘录：${mainText}` : `当前在${title}页面。`,
      items: [],
    };
  }

  private routeLabel(route: string): string {
    if (route.includes('/learn')) return '章节学习页';
    if (/\/courses\/[^/]+$/.test(route)) return '课程详情页';
    if (route.includes('/courses')) return '课程中心';
    return '学生端页面';
  }
}
