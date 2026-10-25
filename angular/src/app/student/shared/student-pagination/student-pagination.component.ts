import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';

/**
 * 学生端服务端分页条。
 *
 * 现有学生端多个页面是「拉 maxResultCount:200/100 再客户端 slice」，
 * 数据量上来必然崩。本组件统一走服务端分页：
 *   total / pageIndex / pageSize → 后端 PagedResultDto
 *   pageChange → 页面重新请求
 */
@Component({
  selector: 'app-student-pagination',
  standalone: true,
  imports: [CommonModule, NzPaginationModule],
  templateUrl: './student-pagination.component.html',
  styleUrls: ['./student-pagination.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentPaginationComponent {
  readonly total = input<number>(0);
  readonly pageIndex = input<number>(1);
  readonly pageSize = input<number>(20);
  /** 是否显示每页条数切换 */
  readonly showSizeChanger = input(false);
  /** 可选的每页条数 */
  readonly pageSizeOptions = input<number[]>([10, 20, 50, 100]);
  /** 总数不足一页时隐藏 */
  readonly hideOnSinglePage = input(true);

  /** 页码或每页条数变化 */
  readonly pageChange = output<{ pageIndex: number; pageSize: number }>();

  onPage(pageIndex: number, pageSize: number): void {
    this.pageChange.emit({ pageIndex, pageSize });
  }
}
