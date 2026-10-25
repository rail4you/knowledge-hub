import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import type { ResourceDto } from '../../proxy/resources/models';
import {
  resourceFormatTag,
  resourceSourceName,
  resourceUpdatedAt,
} from './student-resource-labels';

/**
 * 学生端资源行（资源中心 / 我的收藏 共用同一套行样式）。
 *
 * 对齐原型 `.resource-row`：左侧格式角标 + 中部（专业/模块标签 + 名称 + 来源/更新日期）
 * + 右侧（收藏 + 查看详情）。
 *
 * 行内所有字段都来自真实 API（ResourceDto），不造假数据。
 */
@Component({
  selector: 'app-student-resource-row',
  standalone: true,
  imports: [CommonModule, DatePipe, RouterModule, NzIconModule],
  templateUrl: './student-resource-row.component.html',
  styleUrls: ['./student-resource-row.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentResourceRowComponent {
  /** 行数据（真实 ResourceDto） */
  readonly resource = input.required<ResourceDto>();
  /** 当前用户是否已收藏该资源 */
  readonly collected = input(false);
  /** 当前租户名（本租户资源来源的兜底展示） */
  readonly tenantName = input<string | null>(null);
  /** 是否展示收藏按钮 */
  readonly showCollect = input(true);

  /** 点击收藏按钮：抛出资源 Id，由页面负责调用真实接口 */
  readonly toggleCollect = output<string>();

  /** 格式角标 */
  readonly formatTag = computed(() => resourceFormatTag(this.resource()));

  /** 专业名（多专业时取第一个；后端 majorNames 为真实字段） */
  readonly majorName = computed(() => {
    const r = this.resource();
    if (!r) return '';
    if (r.majorName) return r.majorName;
    const names = r.majorNames || [];
    return names.length ? names.join('、') : '';
  });

  /** 模块（分类）名 */
  readonly categoryName = computed(() => this.resource()?.categoryName || '');

  /** 来源 */
  readonly sourceName = computed(() => resourceSourceName(this.resource(), this.tenantName()));

  /** 更新日期 */
  readonly updatedAt = computed(() => resourceUpdatedAt(this.resource()));

  /** 许可 / 下载权限说明（真实字段：版权信息 + 是否允许下载） */
  readonly licenseText = computed(() => {
    const r = this.resource();
    const copyright = (r?.copyrightInfo || '').trim();
    if (copyright) return copyright;
    if (!r) return '';
    return r.isDownloadable === false ? '仅在线预览' : '允许下载';
  });

  /** 收藏按钮文案 */
  readonly collectLabel = computed(() =>
    this.collected() ? '取消收藏' : `收藏 ${this.resource()?.name || '资源'}`,
  );

  onToggleCollect(): void {
    const id = this.resource()?.id;
    if (!id) return;
    this.toggleCollect.emit(id);
  }
}
