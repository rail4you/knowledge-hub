import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzIconModule } from 'ng-zorro-antd/icon';

/**
 * 学生端服务失败态。
 *
 * 与 StudentEmptyComponent('offline') 的区别：这是「已经发起了请求且失败」，
 * 必须给出重试入口与可读原因，不能和「本来就没有数据」混为一谈。
 */
@Component({
  selector: 'app-student-error',
  standalone: true,
  imports: [CommonModule, NzIconModule],
  templateUrl: './student-error.component.html',
  styleUrls: ['./student-error.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentErrorComponent {
  /** 错误标题 */
  readonly title = input<string>('加载失败');
  /** 错误详情（后端返回的 message 时应脱敏后再传入） */
  readonly detail = input<string>('');
  /** 是否显示重试按钮 */
  readonly retryable = input(true);
  /** 重试文案 */
  readonly retryLabel = input<string>('重新加载');

  readonly retry = output<void>();
}
