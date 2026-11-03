import { Injectable, signal } from '@angular/core';

/**
 * 评估管理的跨页签上下文。
 *
 * <p>「当前评估项目」是全局状态：用户在「评估项目」页签选了项目后，
 * 切到「填报指标 / 项目配置 / 资料导出」仍应保持同一个项目。
 * 由于切页签会触发路由跳转、组件实例重建，状态不能只放在组件里，
 * 这里放到 root 注入的单例服务中保存。</p>
 */
@Injectable({ providedIn: 'root' })
export class AssessmentContextService {
  /** 当前评估项目 id（null = 未选择） */
  readonly currentProjectId = signal<string | null>(null);
}
