import { Injectable, signal } from '@angular/core';

/**
 * 当前课程（跨页面共享，并持久化）。
 *
 * <p>老师在做课程内容维护时，会在「课程工作台 → 章节 / 习题 / 资源 / 教案 / 案例 / 试卷」
 * 之间来回切换。如果每个页面各选各的课，来回切一次就要重选一次，很容易在 A 课程的章节里
 * 误改 B 课程的内容。</p>
 *
 * <p>这里把「当前课程」提到全局：</p>
 * <ul>
 *   <li>课程工作台选中课程 → 写入本上下文；</li>
 *   <li>进入任意课程内容页 → 没有 ?courseId= 时沿用这里的课程；</li>
 *   <li>在内容页里换了课程 → 这里同步跟着换。</li>
 * </ul>
 *
 * <p>写入 localStorage：整页刷新 / 新标签页打开时也能保持选中的课程。</p>
 */
const STORAGE_KEY = 'kh_current_course_id';

@Injectable({ providedIn: 'root' })
export class CourseContextService {
  /** 当前选中的课程 id（null = 未选择） */
  readonly currentCourseId = signal<string | null>(this.readStored());

  setCourse(courseId: string | null): void {
    const id = courseId || null;
    this.currentCourseId.set(id);
    try {
      if (id) {
        localStorage.setItem(STORAGE_KEY, id);
      } else {
        localStorage.removeItem(STORAGE_KEY);
      }
    } catch {
      // 隐私模式等场景禁用了 storage，忽略即可，不影响课程选择
    }
  }

  private readStored(): string | null {
    try {
      return localStorage.getItem(STORAGE_KEY) || null;
    } catch {
      return null;
    }
  }
}
