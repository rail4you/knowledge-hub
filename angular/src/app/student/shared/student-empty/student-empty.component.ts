import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzIconModule } from 'ng-zorro-antd/icon';

/**
 * 学生端空状态。
 *
 * 原型只有一种 `.empty`（虚线框居中），但真实系统必须区分四种语义，
 * 否则用户无法判断「是我的问题」还是「系统的问题」：
 *   data      —— 还没有数据（可引导去学习/创建）
 *   result    —— 筛选条件无结果（可重置筛选）
 *   permission—— 无权限访问该资源（不能重试）
 *   offline   —— 服务或网络不可用（可重试）
 */
export type StudentEmptyVariant = 'data' | 'result' | 'permission' | 'offline';

const DEFAULT_COPY: Record<StudentEmptyVariant, { icon: string; title: string; desc: string }> = {
  data: {
    icon: 'inbox',
    title: '暂无内容',
    desc: '完成学习或提交成果后，这里会自动归集内容。',
  },
  result: {
    icon: 'search',
    title: '没有匹配的结果',
    desc: '当前筛选条件过窄，试试放宽条件或重置筛选。',
  },
  permission: {
    icon: 'lock',
    title: '没有访问权限',
    desc: '该内容不在你的授权范围内。如需访问请联系所属学校或任课教师。',
  },
  offline: {
    icon: 'wifi',
    title: '服务暂时不可用',
    desc: '请稍后重试。若持续失败，请联系平台管理员。',
  },
};

@Component({
  selector: 'app-student-empty',
  standalone: true,
  imports: [CommonModule, NzIconModule],
  templateUrl: './student-empty.component.html',
  styleUrls: ['./student-empty.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentEmptyComponent {
  readonly variant = input<StudentEmptyVariant>('data');
  /** 覆盖默认标题 */
  readonly title = input<string>('');
  /** 覆盖默认说明 */
  readonly description = input<string>('');
  /** 主操作按钮文案；为空则不显示按钮 */
  readonly actionLabel = input<string>('');
  /** 图标覆盖 */
  readonly icon = input<string>('');

  /** 主操作触发 */
  readonly action = output<void>();

  iconName(): string {
    return this.icon() || DEFAULT_COPY[this.variant()].icon;
  }

  titleText(): string {
    return this.title() || DEFAULT_COPY[this.variant()].title;
  }

  descText(): string {
    return this.description() || DEFAULT_COPY[this.variant()].desc;
  }
}
